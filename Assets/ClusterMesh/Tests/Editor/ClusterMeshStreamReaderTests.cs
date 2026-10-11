using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshStreamReaderTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Type ReaderType = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshStreamReader");
        delegate void ReadRange(long offset, byte[] buffer, int length);
        string directory, path;
        byte[] source;
        IDisposable reader;
        ReadRange read;

        [SetUp] public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "ClusterMeshReader-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "Fixture.cmstream");
            source = new byte[262144];
            new Random(4729).NextBytes(source);
            File.WriteAllBytes(path, source);
            reader = (IDisposable)Activator.CreateInstance(ReaderType, Private, null, new object[] { path }, null);
            read = (ReadRange)ReaderType.GetMethod("Read", Private).CreateDelegate(typeof(ReadRange), reader);
        }
        [TearDown] public void TearDown()
        {
            reader?.Dispose();
            if (directory != null) Directory.Delete(directory, true);
        }
        int OpenCount => (int)ReaderType.GetProperty("OpenCount", Private).GetValue(reader);
        object Stream => ReaderType.GetField("stream", Private).GetValue(reader);

        [Test] public void LazyHandleAndBufferAreReusedAcrossChangingOffsets()
        {
            Assert.That(Stream, Is.Null);
            var buffer = new byte[8192];
            read(0, buffer, buffer.Length);
            object first = Stream;
            for (int n = 0; n < 256; n++)
            {
                int offset = n * 941 % (source.Length - buffer.Length);
                read(offset, buffer, buffer.Length);
                for (int i = 0; i < buffer.Length; i++)
                    if (buffer[i] != source[offset + i]) Assert.Fail("Read range differs at " + (offset + i));
                Assert.That(Stream, Is.SameAs(first));
            }
            Assert.That(OpenCount, Is.EqualTo(1));
            reader.Dispose();
            Assert.That(Stream, Is.Null);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }

        [Test] public void ConcurrentWorkersCannotMixSeekPositionsOrPayloads()
        {
            var tasks = new Task[8];
            for (int i = 0; i < tasks.Length; i++)
            {
                int worker = i;
                tasks[i] = Task.Run(() =>
                {
                    var buffer = new byte[4093 + worker * 37];
                    for (int n = 0; n < 128; n++)
                    {
                        int offset = (n * 15013 + worker * 2707) % (source.Length - buffer.Length);
                        read(offset, buffer, buffer.Length);
                        for (int at = 0; at < buffer.Length; at++)
                            if (buffer[at] != source[offset + at]) throw new InvalidDataException("Concurrent range alias");
                    }
                });
            }
            Assert.That(Task.WaitAll(tasks, 15000), Is.True);
            Assert.That(OpenCount, Is.EqualTo(1));
        }

        [Test] public void InvalidRangesAndFaultedReadsDoNotLeakHandles()
        {
            var buffer = new byte[16];
            Assert.Throws<IOException>(() => read(-1, buffer, 1));
            Assert.Throws<IOException>(() => read(0, buffer, 17));
            Assert.Throws<IOException>(() => read(0, null, 1));
            Assert.Throws<IOException>(() => read(0, buffer, -1));
            Assert.That(OpenCount, Is.Zero);
            Assert.Throws<EndOfStreamException>(() => read(long.MaxValue, buffer, 1));
            Assert.That(Stream, Is.Null);
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            read(17, buffer, buffer.Length);
            Assert.That(buffer[0], Is.EqualTo(source[17]));
            reader.Dispose(); reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => read(0, buffer, 1));
        }

        [Test] public void DisposeBeforeFirstReadDoesNotOpenFile()
        {
            reader.Dispose(); reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => read(0, new byte[16], 16));
            Assert.That(OpenCount, Is.Zero);
        }

        sealed class BlockingStream : FileStream
        {
            internal readonly ManualResetEventSlim entered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim resume = new ManualResetEventSlim();
            internal BlockingStream(string path) : base(path, FileMode.Open, FileAccess.Read, FileShare.Read) { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                entered.Set();
                if (!resume.Wait(15000)) throw new TimeoutException("Blocked test read was not released");
                return base.Read(buffer, offset, count);
            }
        }

        [Test] public void DisposeDoesNotWaitForInflightIoAndRejectsQueuedReaders()
        {
            var blocked = new BlockingStream(path);
            ReaderType.GetField("stream", Private).SetValue(reader, blocked);
            Task reading = null, queued = null, disposing = null;
            try
            {
                reading = Task.Run(() => Assert.Throws<ObjectDisposedException>(() => read(0, new byte[128], 128)));
                Assert.That(blocked.entered.Wait(5000), Is.True);
                queued = Task.Run(() => Assert.Throws<ObjectDisposedException>(() => read(128, new byte[128], 128)));
                disposing = Task.Run(() => reader.Dispose());
                Assert.That(disposing.Wait(1000), Is.True, "Unload must not wait for a blocked disk read");
                blocked.resume.Set();
                Assert.That(Task.WaitAll(new[] { reading, queued }, 5000), Is.True);
                Assert.That(Stream, Is.Null);
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
            finally
            {
                blocked.resume.Set();
                if (disposing != null) disposing.Wait(5000);
                if (reading != null) reading.Wait(5000);
                if (queued != null) queued.Wait(5000);
                blocked.Dispose(); blocked.entered.Dispose(); blocked.resume.Dispose();
            }
        }

        [Test] public void WarmReadAllocationEventsAreLowerThanPerPageOpen()
        {
            Assert.That(ClusterMeshGcTests.Measure(() => GC.KeepAlive(new byte[1024]), 1), Is.GreaterThan(0));
            var buffer = new byte[4096];
            Action before = () =>
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    65536, FileOptions.Asynchronous | FileOptions.RandomAccess))
                { file.Seek(1024, SeekOrigin.Begin); file.Read(buffer, 0, buffer.Length); }
            };
            Action after = () => read(1024, buffer, buffer.Length);
            for (int i = 0; i < 16; i++) { before(); after(); }
            long original = ClusterMeshGcTests.Measure(before, 128);
            long reused = ClusterMeshGcTests.Measure(after, 128);
            TestContext.Out.WriteLine($"128 read operations: per-page open={original} allocation events; retained reader={reused} allocation events (not bytes).");
            Assert.That(reused, Is.LessThan(original));
            Assert.That(OpenCount, Is.EqualTo(1));
        }
    }
}
