using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshGpuClearTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
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
                while (metadata != null && !metadata.IsCompleted && EditorApplication.timeSinceStartup < until) yield return null;
                Assert.That(metadata == null || metadata.IsCompleted, Is.True, "Disposed metadata read did not finish");
            }
            finally
            {
                context?.Dispose(); ClusterMeshStreaming.Release(asset);
                UnityEngine.Object.DestroyImmediate(mesh); AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
