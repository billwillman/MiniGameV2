using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshGcTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        Mesh mesh;
        ClusterMeshAsset asset;
        GameObject cameraObject;
        Camera camera;
        ClusterMeshDrawContext context;

        [SetUp]
        public void SetUp()
        {
            Assert.That(Measure(() => GC.KeepAlive(new byte[1024]), 1), Is.GreaterThanOrEqualTo(1),
                "Allocation recorder must detect a known allocation; zero from an unsupported counter is not evidence.");
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null) Assert.Ignore(unsupported);
            mesh = ClusterMeshTestMeshes.Triangle();
            var settings = new ClusterMeshBakeSettings { buildLodHierarchy = false };
            asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            asset.CopyFrom(ClusterMeshBaker.Bake(mesh, new Material[1], settings), mesh, settings);
            cameraObject = new GameObject("ClusterMesh GC test camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            context = new ClusterMeshDrawContext(asset,
                AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute"),
                Shader.Find("ClusterMesh/Lit"));
            Assert.That(context.IsReady, Is.True, context.Error);
        }

        [TearDown]
        public void TearDown()
        {
            context?.Dispose();
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (asset != null) Object.DestroyImmediate(asset);
            if (mesh != null) Object.DestroyImmediate(mesh);
        }

        [TestCase(1, false)]
        [TestCase(257, true)]
        public void MeasurePrepareAllocations(int count, bool motion)
        {
            var matrices = new Matrix4x4[count];
            var flags = new bool[count];
            var motionFlags = new bool[count];
            for (int i = 0; i < count; i++)
            { matrices[i] = Matrix4x4.Translate(new Vector3(i * .01f, 0, 5)); motionFlags[i] = motion; }
            for (int i = 0; i < 8; i++)
                context.PrepareUrpMotion(matrices, matrices, motionFlags, flags, flags, camera, false, true);
            const int iterations = 64;
            long allocations = Measure(() => context.PrepareUrpMotion(matrices, matrices, motionFlags, flags, flags, camera, false, true), iterations);
            TestContext.Out.WriteLine($"Prepare objects={count}, motion={motion}: {allocations / iterations} allocations/call ({iterations} calls)");
            // Baseline and optimized versions can run the same measurement.
            // Only the optimized implementation has a retained chunk cache.
            if (typeof(ClusterMeshDrawContext).GetField("_chunkCache", Private) != null)
                Assert.That(allocations, Is.Zero, "Warm managed draw preparation must not allocate.");
        }

        internal static long Measure(Action action, int iterations)
        {
            bool wasEnabled = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            try
            {
              using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 65536,
                       ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
              {
                for (int i = 0; i < iterations; i++) action();
                recorder.Stop();
                Assert.That(recorder.Valid, Is.True);
                Assert.That(recorder.Count, Is.LessThan(recorder.Capacity), "Recorder capacity exhausted.");
                // GC.Alloc is a marker: Value is duration, not allocation bytes.
                // Non-summed samples count allocation events on this thread.
                return recorder.Count;
              }
            }
            finally { UnityEngine.Profiling.Profiler.enabled = wasEnabled; }
        }

        IList Chunks(string name) => (IList)typeof(ClusterMeshDrawContext).GetField(name, Private).GetValue(context);
        static T ChunkField<T>(object chunk, string name) => (T)chunk.GetType().GetField(name).GetValue(chunk);

        [Test]
        public void ChunksReuseStorageButOnlySubmitCurrentObjectsAndMotion()
        {
            var matrices = new Matrix4x4[257];
            var flags = new bool[257];
            var motion = new bool[257];
            for (int i = 0; i < matrices.Length; i++) { matrices[i] = Matrix4x4.identity; motion[i] = true; }
            Assert.That(context.PrepareUrpMotion(matrices, matrices, motion, flags, flags, camera, false, true), Is.True);
            var active = Chunks("_urpChunks");
            Assert.That(active.Count, Is.EqualTo(2));
            object first = active[0], second = active[1];
            var original = ChunkField<Matrix4x4[]>(first, "l2w");
            var previous = ChunkField<Matrix4x4[]>(first, "previousL2w");
            var extra = ChunkField<GraphicsBuffer[]>(second, "visible")[0];
            matrices[0] = Matrix4x4.Translate(new Vector3(17, 0, 5));
            var single = new[] { matrices[0] };
            context.PrepareUrp(single, flags, flags, camera, false, true);
            Assert.That(active.Count, Is.EqualTo(1));
            Assert.That(active[0], Is.SameAs(first));
            Assert.That(ChunkField<int>(first, "n"), Is.EqualTo(1));
            Assert.That(ChunkField<bool>(first, "hasMotionVectors"), Is.False);
            Assert.That(ChunkField<Matrix4x4[]>(first, "l2w"), Is.SameAs(original));
            Assert.That(original[0], Is.EqualTo(matrices[0]));
            Assert.That(ChunkField<Matrix4x4[]>(first, "previousL2w"), Is.SameAs(previous));
            Assert.That(context.PrepareUrp(Array.Empty<Matrix4x4>(), flags, flags, camera, false, true), Is.False);
            Assert.That(active.Count, Is.Zero);
            context.PrepareUrpMotion(matrices, matrices, motion, flags, flags, camera, false, true);
            Assert.That(active[1], Is.SameAs(second));
            Assert.That(ChunkField<GraphicsBuffer[]>(second, "visible")[0], Is.SameAs(extra));
            using (var cmd = new CommandBuffer())
            { context.SubmitUrpDepth(cmd); context.SubmitUrpMotionVectors(cmd); Graphics.ExecuteCommandBuffer(cmd); }
            for (int i = 0; i < 121; i++) context.PrepareUrp(single, flags, flags, camera, false, true);
            Assert.That(Chunks("_chunkCache").Count, Is.EqualTo(1));
            Assert.That(extra.IsValid(), Is.False, "Unused extra GPU buffer must be released.");
            context.Dispose(); context.Dispose();
            Assert.That(Chunks("_chunkCache").Count, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FullyCulledFirstBatchUsesPrimaryBuffersForTheSurvivor()
        {
            var matrices = new Matrix4x4[257];
            var flags = new bool[257];
            for (int i = 0; i < matrices.Length; i++)
            { matrices[i] = Matrix4x4.Translate(new Vector3(100000, 0, 5)); flags[i] = true; }
            matrices[256] = Matrix4x4.Translate(new Vector3(0, 0, 5));
            Assert.That(context.PrepareUrp(matrices, flags, flags, camera, false, true), Is.True);
            Assert.That(Chunks("_urpChunks").Count, Is.EqualTo(1));
            Assert.That(Chunks("_chunkCache").Count, Is.EqualTo(1));
            var primary = (GraphicsBuffer[])typeof(ClusterMeshDrawContext).GetField("_visibleBuffers", Private).GetValue(context);
            Assert.That(ChunkField<GraphicsBuffer[]>(Chunks("_urpChunks")[0], "visible")[0], Is.SameAs(primary[0]));
        }

    }
}
