using System;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("ClusterMesh.Editor.Tests")]

namespace ClusterMesh
{
    // Only for synchronous IO/decoding on background threads. No Unity API or
    // caller ExecutionContext is carried into a page read. Capacity includes
    // completed-but-uncollected work and retiring threads, not just queued IO.
    internal sealed class ClusterMeshReadWorkPool<TInput, TResult> where TResult : class, IDisposable
    {
        internal const int MaximumWorkers = 32;
        internal const int RequestedStackBytes = 256 * 1024;
        readonly object gate = new object();
        readonly Func<TInput, TResult> execute;
        readonly Work[] idle = new Work[MaximumWorkers];
        int idleCount, count, inUse, limit = MaximumWorkers, generation;

        internal ClusterMeshReadWorkPool(Func<TInput, TResult> execute) { this.execute = execute; }
        internal int WorkerCount { get { lock (gate) return count; } }
        internal int InUse { get { lock (gate) return inUse; } }
        internal void SetLimit(int value)
        {
            lock (gate)
            {
                limit = Math.Max(0, Math.Min(MaximumWorkers, value));
                while (idleCount > Math.Max(0, limit - inUse)) Pop().Stop();
            }
        }
        Work Pop() { var work = idle[--idleCount]; idle[idleCount] = null; return work; }

        internal Work TryStart(TInput input)
        {
            Work work;
            lock (gate)
            {
                if (inUse >= limit) return null;
                if (idleCount > 0) work = Pop();
                else
                {
                    if (count >= limit) return null;
                    work = new Work(this, generation);
                    count++;
                    try { work.StartThread(); }
                    catch { count--; throw; }
                }
                inUse++;
            }
            work.Schedule(input);
            return work;
        }
        void Return(Work work)
        {
            lock (gate)
            {
                inUse--;
                if (work.generation != generation || idleCount + inUse >= limit) work.Stop();
                else idle[idleCount++] = work;
            }
        }
        // Called after owners abandon their outstanding reads. Does not wait
        // for disk IO, cancel a thread, or release another owner's payload.
        internal void Clear()
        {
            lock (gate)
            {
                generation++;
                while (idleCount > 0) Pop().Stop();
            }
        }

        internal sealed class Work
        {
            readonly ClusterMeshReadWorkPool<TInput, TResult> pool;
            readonly object gate = new object();
            internal readonly int generation;
            TInput input;
            TResult result;
            Exception error;
            bool pending, abandoned, returned = true, stopping;
            int completed;
            internal bool IsCompleted => Volatile.Read(ref completed) != 0;
            internal Work(ClusterMeshReadWorkPool<TInput, TResult> pool, int generation)
            { this.pool = pool; this.generation = generation; }
            internal void StartThread()
            {
                var thread = new Thread(Run, RequestedStackBytes) { IsBackground = true, Name = "ClusterMesh Page IO" };
                if (ExecutionContext.IsFlowSuppressed()) thread.Start();
                else using (ExecutionContext.SuppressFlow()) thread.Start();
            }
            internal void Schedule(TInput value)
            {
                lock (gate)
                {
                    input = value; result = null; error = null;
                    abandoned = returned = false; Volatile.Write(ref completed, 0);
                    pending = true; Monitor.Pulse(gate);
                }
            }
            internal void Stop() { lock (gate) { stopping = true; Monitor.Pulse(gate); } }
            internal TResult TakeResult(out Exception failure)
            {
                TResult value;
                lock (gate)
                {
                    if (!IsCompleted || returned) throw new InvalidOperationException("Read is not owned/completed.");
                    returned = true; value = result; failure = error; result = null; error = null;
                }
                pool.Return(this);
                return value;
            }
            internal void Abandon()
            {
                TResult value;
                lock (gate)
                {
                    if (returned) return;
                    abandoned = true;
                    if (!IsCompleted) return;
                    returned = true; value = result; result = null; error = null;
                }
                try { value?.Dispose(); } finally { pool.Return(this); }
            }
            void Run()
            {
                try
                {
                    while (true)
                    {
                        TInput next;
                        lock (gate)
                        {
                            while (!pending && !stopping) Monitor.Wait(gate);
                            if (stopping) return;
                            next = input; input = default; pending = false;
                        }
                        TResult value = null; Exception failure = null;
                        try { value = pool.execute(next); }
                        catch (Exception ex) { failure = ex; }
                        next = default; // No idle reader, inflater, or asset references.
                        bool release;
                        lock (gate)
                        {
                            release = abandoned;
                            if (release) returned = true;
                            else { result = value; error = failure; }
                            Volatile.Write(ref completed, 1);
                        }
                        failure = null;
                        if (release)
                        {
                            try { value?.Dispose(); } finally { pool.Return(this); }
                        }
                        value = null;
                    }
                }
                finally { lock (pool.gate) pool.count--; }
            }
        }
    }
}
