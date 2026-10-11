using System;
using System.IO;
using System.Threading;

namespace ClusterMesh
{
    // One lazily opened handle/buffer per live streamed asset, not per page.
    // Called ONLY from page/metadata workers. Seek + Read on a shared FileStream
    // must be atomic; inflate/CRC happen outside this gate and remain parallel.
    // No global path cache: disposing an owner also retires its file handle.
    internal sealed class ClusterMeshStreamReader : IDisposable
    {
        readonly string path;
        readonly object gate = new object();
        FileStream stream;
        int disposed;
        internal int OpenCount { get; private set; }
        // Fixed FileStream buffer per opened reader (0.0625 MiB), NOT per
        // request/worker. Caller-owned page/metadata arrays are additional.
        // Reads larger than this buffer do not increase the configured size.
        internal const int BufferBytes = 65536;

        internal ClusterMeshStreamReader(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Missing stream path", nameof(path));
            this.path = path;
        }

        internal void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(ClusterMeshStreamReader));
        }

        internal void Read(long offset, byte[] result, int length)
        {
            if (offset < 0 || length < 0 || result == null || result.Length < length)
                throw new IOException("ClusterMesh stream range is invalid.");
            lock (gate)
            {
                try
                {
                    ThrowIfDisposed();
                    if (stream == null)
                    {
                        // Synchronous IO on a worker avoids FileStream async
                        // wrappers/tasks as well as repeated 64 KiB buffers.
                        stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                            BufferBytes, FileOptions.RandomAccess);
                        OpenCount++;
                    }
                    if (offset > stream.Length - length)
                        throw new EndOfStreamException("ClusterMesh stream range exceeds file.");
                    stream.Seek(offset, SeekOrigin.Begin);
                    int cursor = 0;
                    while (cursor < length)
                    {
                        ThrowIfDisposed();
                        int read = stream.Read(result, cursor, length - cursor);
                        if (read <= 0) throw new EndOfStreamException("ClusterMesh stream ended unexpectedly.");
                        cursor += read;
                    }
                    ThrowIfDisposed();
                }
                catch
                {
                    Close(); // Never reuse a handle after a failed read.
                    throw;
                }
                finally
                {
                    if (Volatile.Read(ref disposed) != 0) Close();
                }
            }
        }

        void Close()
        {
            var old = stream;
            stream = null;
            old?.Dispose();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            // Main-thread unload must not wait on disk IO. If a worker owns the
            // gate, its finally closes the stream; queued workers are rejected.
            if (!Monitor.TryEnter(gate)) return;
            try { Close(); }
            finally { Monitor.Exit(gate); }
        }
    }
}
