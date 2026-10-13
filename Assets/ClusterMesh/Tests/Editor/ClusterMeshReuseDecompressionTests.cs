using System;
using System.IO;
using System.IO.Compression;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshReuseDecompressionTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static Action<byte[], int, int, byte[], int> Decoder()
        {
            var type = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshPageInflater", true);
            var target = Activator.CreateInstance(type, true);
            return (Action<byte[], int, int, byte[], int>)Delegate.CreateDelegate(
                typeof(Action<byte[], int, int, byte[], int>), target, type.GetMethod("Inflate", Hidden));
        }
        static byte[] Compress(byte[] data, CompressionLevel level)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new DeflateStream(output, level, true)) zip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(65535)] [TestCase(185356)]
        public void IndependentPagesAndFailuresDoNotRetainDecoderState(int size)
        {
            var inflate = Decoder(); var raw = new byte[size]; var actual = new byte[size + 1];
            var random = new System.Random(1951);
            foreach (CompressionLevel level in new[] { CompressionLevel.NoCompression, CompressionLevel.Fastest, CompressionLevel.Optimal })
            {
                for (int mode = 0; mode < 3; mode++)
                {
                    if (mode == 2) random.NextBytes(raw);
                    else for (int i = 0; i < raw.Length; i++) raw[i] = (byte)(mode == 0 ? 0 : i % 71);
                    byte[] packed = Compress(raw, level);
                    // Some system compressors emit no raw stream for empty input.
                    if (packed.Length == 0) packed = new byte[] { 3, 0 };
                    inflate(packed, 0, packed.Length, actual, size);
                    for (int i = 0; i < size; i++) if (actual[i] != raw[i]) Assert.Fail("Decoded byte " + i);
                    Assert.Throws<InvalidDataException>(() => inflate(packed, 0, packed.Length - 1, actual, size));
                    Assert.Throws<InvalidDataException>(() => inflate(packed, 0, packed.Length, actual, size + 1));
                    inflate(packed, 0, packed.Length, actual, size);
                }
            }
        }
        [Test]
        public void WarmDynamicPageInflateHasNoManagedAllocations()
        {
            var inflate = Decoder(); var raw = new byte[65536]; var decoded = new byte[raw.Length];
            var random = new System.Random(45);
            for (int i = 0; i < raw.Length; i++) raw[i] = (byte)(random.Next(32));
            byte[] packed = Compress(raw, CompressionLevel.Optimal);
            Action run = () => inflate(packed, 0, packed.Length, decoded, raw.Length);
            for (int i = 0; i < 8; i++) run();
            Assert.That(decoded, Is.EqualTo(raw));
            Assert.That(ClusterMeshGcTests.Measure(() => GC.KeepAlive(new byte[4096]), 1), Is.GreaterThan(0));
            Assert.That(ClusterMeshGcTests.Measure(run, 64), Is.Zero);
        }
        const BindingFlags StaticHidden = BindingFlags.Static | BindingFlags.NonPublic;
        static readonly Type Pool = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshInflatePool", true);
        bool previousMode;
        int previousLimit;

        [SetUp] public void SetUp()
        {
            Assert.That(ClusterMeshStreaming.ActiveDecodeContexts, Is.Zero);
            previousMode = ClusterMeshStreaming.ReuseDecompressionContexts;
            previousLimit = ClusterMeshStreaming.MaxConcurrentReads;
            ClusterMeshStreaming.ReuseDecompressionContexts = false;
            Pool.GetMethod("Clear", StaticHidden).Invoke(null, null);
        }

        [TearDown] public void TearDown()
        {
            ClusterMeshStreaming.ReuseDecompressionContexts = false;
            Assert.That(ClusterMeshStreaming.ActiveDecodeContexts, Is.Zero);
            Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.Zero);
            ClusterMeshStreaming.MaxConcurrentReads = previousLimit;
            ClusterMeshStreaming.ReuseDecompressionContexts = previousMode;
        }

        static object Rent()
        {
            var args = new object[] { null };
            return (bool)Pool.GetMethod("TryRent", StaticHidden).Invoke(null, args) ? args[0] : null;
        }
        static void Return(object context) => Pool.GetMethod("Return", StaticHidden).Invoke(null, new[] { context });

        [Test] public void NewSettingsDefaultToNativeWithoutRetainedWorkspaces()
        {
            var settings = ScriptableObject.CreateInstance<ClusterMeshSettings>();
            try
            {
                Assert.That(settings.ReuseDecompressionContexts, Is.False);
                Assert.That(Rent(), Is.Null);
                Assert.That(ClusterMeshStreaming.DecodeWorkspacePayloadBytes, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test] public void DisableDropsIdleAndReturningWorkersWithoutInterruptingTheirLease()
        {
            ClusterMeshStreaming.MaxConcurrentReads = 2;
            ClusterMeshStreaming.ReuseDecompressionContexts = true;
            object first = Rent(), second = Rent();
            try
            {
                Assert.That(first, Is.Not.Null); Assert.That(second, Is.Not.Null);
                Assert.That(Rent(), Is.Null, "Bound must follow concurrent reads");
                Return(first); first = null;
                Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.EqualTo(2));
                ClusterMeshStreaming.ReuseDecompressionContexts = false;
                Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.EqualTo(1));
                Assert.That(Rent(), Is.Null);
                // The leased worker can still decode after the switch is off.
                second.GetType().GetMethod("Inflate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(second, new object[] { new byte[] { 3, 0 }, 0, 2, new byte[0], 0 });
            }
            finally { if (first != null) Return(first); if (second != null) Return(second); }
            Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.Zero);
        }

        [Test] public void ShrinkAndReenableRespectOutstandingWorkers()
        {
            ClusterMeshStreaming.MaxConcurrentReads = 2;
            ClusterMeshStreaming.ReuseDecompressionContexts = true;
            object first = Rent(), second = Rent(), next = null;
            try
            {
                ClusterMeshStreaming.ReuseDecompressionContexts = false;
                ClusterMeshStreaming.MaxConcurrentReads = 1;
                ClusterMeshStreaming.ReuseDecompressionContexts = true;
                Assert.That(Rent(), Is.Null);
                Return(first); first = null;
                Assert.That(Rent(), Is.Null);
                Return(second); second = null;
                Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.EqualTo(1));
                next = Rent(); Assert.That(next, Is.Not.Null);
                Assert.That(ClusterMeshStreaming.DecodeWorkspacePayloadBytes, Is.EqualTo(3174));
            }
            finally { if (first != null) Return(first); if (second != null) Return(second); if (next != null) Return(next); }
        }

        [TestCase(false)] [TestCase(true)]
        public void CorruptPageTaskReturnsLeaseForSingleAndBatchReads(bool batch)
        {
            ClusterMeshStreaming.MaxConcurrentReads = 1;
            ClusterMeshStreaming.ReuseDecompressionContexts = true;
            string path = Path.GetTempFileName();
            var readerType = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshStreamReader", true);
            object reader = null, context = Rent(); Task task = null;
            try
            {
                File.WriteAllBytes(path, new byte[] { 7, 0 }); // Reserved DEFLATE block type.
                reader = Activator.CreateInstance(readerType, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { path }, null);
                var page = new ClusterMeshStreamPage { fileOffset = 0, compressedLength = 2, uncompressedLength = 32 };
                var start = typeof(ClusterMeshPageRuntime).GetMethod(batch ? "StartRootBatchRead" : "StartPageRead", StaticHidden);
                task = (Task)start.Invoke(null, new object[] { reader, batch ? (object)new[] { page } : page, 32, 0, context });
                context = null; // The worker now owns the lease, including failure cleanup.
                Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(10)));
                Assert.That(task.IsFaulted, Is.True);
                Assert.That(task.Exception.InnerException, Is.TypeOf<InvalidDataException>());
                Assert.That(ClusterMeshStreaming.ActiveDecodeContexts, Is.Zero);
                Assert.That(ClusterMeshStreaming.RetainedDecodeContexts, Is.EqualTo(1));
            }
            finally
            {
                if (context != null) Return(context);
                (reader as IDisposable)?.Dispose();
                if (task != null && !task.IsCompleted) { try { task.Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException) { } }
                File.Delete(path);
            }
        }
    }
}
