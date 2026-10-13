using System;
using System.Threading;
using NUnit.Framework;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshReadWorkPoolTests
    {
        sealed class Result : IDisposable
        {
            public int disposals, thread;
            public bool hadContext;
            public void Dispose() { Interlocked.Increment(ref disposals); }
        }
        struct Input { public Result value; public ManualResetEventSlim wait; public bool fail; }
        static readonly AsyncLocal<string> Local = new AsyncLocal<string>();
        ClusterMeshReadWorkPool<Input, Result> pool;
        static Result Read(Input input)
        {
            input.wait?.Wait();
            if (input.fail) throw new InvalidOperationException("Expected read failure");
            input.value.thread = Thread.CurrentThread.ManagedThreadId;
            input.value.hadContext = SynchronizationContext.Current != null || Local.Value != null;
            return input.value;
        }
        [SetUp] public void SetUp() { pool = new ClusterMeshReadWorkPool<Input, Result>(Read); pool.SetLimit(1); }
        [TearDown] public void TearDown()
        {
            pool.Clear();
            Assert.That(SpinWait.SpinUntil(() => pool.WorkerCount == 0, 10000), Is.True, "Workers must retire without a main-thread join.");
            Assert.That(pool.InUse, Is.Zero);
        }
        static void Wait(ClusterMeshReadWorkPool<Input, Result>.Work work)
        { Assert.That(SpinWait.SpinUntil(() => work.IsCompleted, 10000), Is.True); }

        [Test] public void CompletedWorkStillOccupiesItsLeaseAndReusesTheSameHandle()
        {
            var result = new Result(); var work = pool.TryStart(new Input { value = result }); Wait(work);
            Assert.That(pool.TryStart(default), Is.Null);
            Assert.That(work.TakeResult(out var error), Is.SameAs(result)); Assert.That(error, Is.Null);
            var again = pool.TryStart(new Input { value = result }); Assert.That(again, Is.SameAs(work)); Wait(again);
            again.TakeResult(out error); Assert.That(error, Is.Null); Assert.That(result.disposals, Is.Zero);
        }
        [Test] public void WarmSchedulingAndCollectionAllocateNothingOnCallerThread()
        {
            var input = new Input { value = new Result() };
            Action run = () =>
            {
                var work = pool.TryStart(input);
                if (work == null) throw new Exception("No worker");
                while (!work.IsCompleted) Thread.Yield();
                if (work.TakeResult(out var error) != input.value || error != null) throw new Exception("Invalid result");
            };
            for (int i = 0; i < 64; i++) run();
            Assert.That(ClusterMeshGcTests.Measure(() => GC.KeepAlive(new byte[4096]), 1), Is.GreaterThan(0));
            Assert.That(ClusterMeshGcTests.Measure(run, 512), Is.Zero);
            Assert.That(input.value.thread, Is.Not.EqualTo(Thread.CurrentThread.ManagedThreadId));
        }
        [Test] public void CallerExecutionContextIsNotRetained()
        {
            Local.Value = "caller-only";
            try
            {
                var result = new Result(); var work = pool.TryStart(new Input { value = result }); Wait(work);
                work.TakeResult(out var error); Assert.That(error, Is.Null); Assert.That(result.hadContext, Is.False);
            }
            finally { Local.Value = null; }
        }
        [TestCase(false)] [TestCase(true)] public void AbandonBeforeOrAfterCompletionReturnsPayloadExactlyOnce(bool complete)
        {
            using (var signal = new ManualResetEventSlim(complete))
            {
                var result = new Result(); var work = pool.TryStart(new Input { value = result, wait = signal });
                try { if (complete) Wait(work); work.Abandon(); work.Abandon(); }
                finally { signal.Set(); }
                Assert.That(SpinWait.SpinUntil(() => pool.InUse == 0, 10000), Is.True);
                Assert.That(result.disposals, Is.EqualTo(1));
            }
        }
        [Test] public void FailureIsCollectedAndDoesNotPoisonTheNextRead()
        {
            var work = pool.TryStart(new Input { fail = true }); Wait(work);
            Assert.That(work.TakeResult(out var error), Is.Null); Assert.That(error, Is.TypeOf<InvalidOperationException>());
            var result = new Result(); work = pool.TryStart(new Input { value = result }); Wait(work);
            Assert.That(work.TakeResult(out error), Is.SameAs(result)); Assert.That(error, Is.Null);
        }
        [Test] public void ShrinkDoesNotInterruptWorkOrGrowPastTheBound()
        {
            pool.SetLimit(4);
            using (var signal = new ManualResetEventSlim(false))
            {
                var work = new ClusterMeshReadWorkPool<Input, Result>.Work[4];
                try
                {
                    for (int i = 0; i < work.Length; i++) work[i] = pool.TryStart(new Input { value = new Result(), wait = signal });
                    Assert.That(pool.WorkerCount, Is.EqualTo(4)); Assert.That(pool.TryStart(default), Is.Null);
                    pool.SetLimit(1); Assert.That(pool.TryStart(default), Is.Null);
                }
                finally { foreach (var w in work) w?.Abandon(); signal.Set(); }
                Assert.That(SpinWait.SpinUntil(() => pool.InUse == 0 && pool.WorkerCount <= 1, 10000), Is.True);
            }
        }
        [Test] public void ClearDuringReadRetiresOldGenerationWithoutOvercommitting()
        {
            using (var signal = new ManualResetEventSlim(false))
            {
                var result = new Result(); var work = pool.TryStart(new Input { value = result, wait = signal });
                try { pool.Clear(); Assert.That(pool.TryStart(default), Is.Null); }
                finally { work.Abandon(); signal.Set(); }
                Assert.That(SpinWait.SpinUntil(() => pool.WorkerCount == 0, 10000), Is.True);
                Assert.That(result.disposals, Is.EqualTo(1));
                work = pool.TryStart(new Input { value = new Result() }); Wait(work); work.TakeResult(out var error);
                Assert.That(error, Is.Null);
            }
        }
    }
}
