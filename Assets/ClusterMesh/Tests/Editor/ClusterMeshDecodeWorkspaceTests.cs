using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshDecodeWorkspaceTests
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
        [Test]
        public void ClearKernelHonorsRangeAndDispatchTail()
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Compute shaders required");
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            int kernel = shader.FindKernel("ClearStreamSelectionStamps");
            var seed = new uint[195]; for (int i = 0; i < seed.Length; i++) seed[i] = 91;
            using (var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, seed.Length, 4))
            {
                buffer.SetData(seed);
                shader.SetBuffer(kernel, "_StreamSelectedNodeStamps", buffer);
                shader.SetInt("_StreamClearOffset", 7); shader.SetInt("_StreamClearCount", 136);
                shader.Dispatch(kernel, 3, 1, 1);
                buffer.GetData(seed); // Readback is test-only, never in runtime clear.
                for (int i = 0; i < seed.Length; i++) Assert.That(seed[i], Is.EqualTo(i >= 7 && i < 136 ? 0u : 91u), "index " + i);
            }
        }
        [UnityTest]
        public IEnumerator StreamedContextInitializesAndWrapsStampsOnGpuWithoutZeroArrays()
        {
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null) Assert.Ignore(unsupported);
            string folder = "Assets/ClusterMesh/Tests/__GpuClear" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/ClusterMesh/Tests", Path.GetFileName(folder));
            Mesh mesh = ClusterMeshTestMeshes.Grid(8, 8);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshDrawContext context = null;
            try
            {
                var settings = new ClusterMeshBakeSettings { enableStreaming = true };
                asset.CopyFrom(ClusterMeshBaker.Bake(mesh, new Material[1], settings), mesh, settings);
                string path = folder + "/Stream.asset";
                AssetDatabase.CreateAsset(asset, path); ClusterMeshStreamBaker.FinalizeStatic(asset, path, settings);
                context = new ClusterMeshDrawContext(asset,
                    AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute"), Shader.Find("ClusterMesh/Lit"));
                Assert.That(context.IsReady, Is.True, context.Error);
                var type = typeof(ClusterMeshDrawContext);
                var buffer = (GraphicsBuffer)type.GetField("_streamSelectedNodeStamps", Hidden).GetValue(context);
                var values = new uint[buffer.count]; buffer.GetData(values);
                Assert.That(values, Is.All.EqualTo(0u));
                for (int i = 0; i < values.Length; i++) values[i] = 13;
                buffer.SetData(values);
                type.GetField("_streamSelectionStamp", Hidden).SetValue(context, uint.MaxValue);
                var next = (Action)Delegate.CreateDelegate(typeof(Action), context, type.GetMethod("SetNextStreamSelectionStamp", Hidden));
                next(); buffer.GetData(values);
                Assert.That(values, Is.All.EqualTo(0u));
                Assert.That((uint)type.GetField("_streamSelectionStamp", Hidden).GetValue(context), Is.EqualTo(1u));
                var clear = (Action)Delegate.CreateDelegate(typeof(Action), context, type.GetMethod("ClearStreamSelectionStamps", Hidden));
                clear();
                Assert.That(ClusterMeshGcTests.Measure(() => GC.KeepAlive(new byte[4096]), 1), Is.GreaterThan(0));
                Assert.That(ClusterMeshGcTests.Measure(clear, 16), Is.Zero);
                var runtime = type.GetField("_streamRuntime", Hidden).GetValue(context);
                var metadata = (System.Threading.Tasks.Task)runtime.GetType().GetField("_metadataRead", Hidden).GetValue(runtime);
                context.Dispose(); context = null;
                double until = EditorApplication.timeSinceStartup + 15;
                while ((ClusterMeshStreaming.ActiveDecodeContexts > 0 || (metadata != null && !metadata.IsCompleted)) && EditorApplication.timeSinceStartup < until) yield return null;
                Assert.That(ClusterMeshStreaming.ActiveDecodeContexts, Is.Zero);
            }
            finally
            {
                context?.Dispose(); ClusterMeshStreaming.Release(asset);
                UnityEngine.Object.DestroyImmediate(mesh); AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
