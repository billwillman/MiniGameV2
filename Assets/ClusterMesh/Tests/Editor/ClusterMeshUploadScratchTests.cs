using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshUploadScratchTests
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly Type Scratch = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshUploadScratch");
        static object Acquire(bool tight, int vertices, int indices, int weights, int stride) =>
            Scratch.GetMethod("Acquire", Static).Invoke(null, new object[] { tight, vertices, indices, weights, stride });
        static void Release() => Scratch.GetMethod("Release", Static).Invoke(null, null);
        static void Clear() => Scratch.GetMethod("Clear", Static).Invoke(null, null);
        static Array Buffer(object scratch, string field) => (Array)Scratch.GetField(field, Instance).GetValue(scratch);

        [SetUp] public void SetUp() { Clear(); }
        [TearDown] public void TearDown() { Release(); Clear(); }

        [Test]
        public void SequentialUploads_ReuseBuffersWithoutStaleDataInCopiedRange()
        {
            var a = Acquire(false, 3, 2, 0, 0);
            var vertices = Buffer(a, "vertices"); var indices = (uint[])Buffer(a, "indices");
            indices[0] = 0x12345678; Release();
            var b = Acquire(false, 2, 1, 0, 0);
            Assert.That(Buffer(b, "vertices"), Is.SameAs(vertices));
            Assert.That(Buffer(b, "indices"), Is.SameAs(indices));
            // Exactly the count passed to SetData is overwritten, not cached capacity.
            System.Buffer.BlockCopy(new byte[] { 1, 0, 0, 0 }, 0, indices, 0, 4);
            using (var gpu = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 4))
            {
                gpu.SetData(indices, 0, 0, 1);
                var readback = new uint[1]; gpu.GetData(readback);
                Assert.That(readback[0], Is.EqualTo(1u));
            }
        }

        [Test]
        public void AllFormats_AreBoundedAndExplicitlyClearable()
        {
            var a = Acquire(false, 65536, 131072, 65536, 16); Release();
            var b = Acquire(true, 65536, 131072, 65536, 8); Release();
            Assert.That(Buffer(a, "vertices").Length, Is.EqualTo(65536));
            Assert.That(Buffer(b, "tightVertices").Length, Is.EqualTo(65536));
            Assert.That(Buffer(a, "weights").Length, Is.EqualTo(65536));
            Assert.That(Buffer(b, "weights8").Length, Is.EqualTo(131072));
            Assert.That(ClusterMeshStreaming.UploadScratchBytes, Is.EqualTo(5767168));
            ClusterMeshStreaming.DisposeAll();
            Assert.That(ClusterMeshStreaming.UploadScratchBytes, Is.Zero);
        }

        [Test]
        public void InvalidSizeAndReentrantUse_AreRejected()
        {
            Assert.That(Assert.Throws<TargetInvocationException>(() => Acquire(false, 65537, 1, 0, 0)).InnerException,
                Is.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(ClusterMeshStreaming.UploadScratchBytes, Is.Zero);
            Acquire(false, 3, 2, 0, 0);
            Assert.That(Assert.Throws<TargetInvocationException>(() => Acquire(false, 3, 2, 0, 0)).InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(Assert.Throws<TargetInvocationException>(Clear).InnerException, Is.TypeOf<InvalidOperationException>());
            Release(); Clear();
            Assert.That(ClusterMeshStreaming.UploadScratchBytes, Is.Zero);
        }

        [Test]
        public void CompactChildren_HandleMultipleRootsAndNonContiguousChildren()
        {
            var nodes = new ClusterMeshStreamNode[5];
            int[] parents = { -1, 0, -1, 0, 2 };
            for (int i = 0; i < nodes.Length; i++) nodes[i].parentNodeIndex = parents[i];
            var type = typeof(ClusterMeshStreaming).Assembly.GetType("ClusterMesh.ClusterMeshNodeIndexTable");
            var table = type.GetMethod("Children", Static).Invoke(null, new object[] { nodes });
            Assert.That(type.GetField("offsets", Instance).GetValue(table), Is.EqualTo(new[] { 0, 2, 2, 3, 3, 3 }));
            Assert.That(type.GetField("items", Instance).GetValue(table), Is.EqualTo(new[] { 1, 3, 4 }));
            var counts = new[] { 0, 2, 1, 2 };
            var pages = new ClusterMeshStreamPage[5];
            int[] owners = { 2, 0, 1, 2, 0 };
            for (int i = 0; i < pages.Length; i++) pages[i].nodeIndex = owners[i];
            table = type.GetMethod("Pages", Static).Invoke(null, new object[] { counts, pages });
            Assert.That(type.GetField("offsets", Instance).GetValue(table), Is.SameAs(counts));
            Assert.That(counts, Is.EqualTo(new[] { 0, 2, 3, 5 }));
            Assert.That(type.GetField("items", Instance).GetValue(table), Is.EqualTo(new[] { 1, 4, 2, 0, 3 }));
        }
    }
}
