using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshReadbackRetirementTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void SetUp()
        {
            if (!SystemInfo.supportsAsyncGPUReadback) Assert.Ignore("GPU readback required");
            ClusterMeshReadbackRetirement.Drain();
            ClusterMeshReadbackRetirement.Resume();
        }
        [TearDown] public void TearDown()
        {
            ClusterMeshReadbackRetirement.Drain();
            ClusterMeshReadbackRetirement.Resume();
            Assert.That(ClusterMeshReadbackRetirement.PendingCount, Is.Zero);
        }
        static GraphicsBuffer[] Buffers(int count)
        {
            var buffers = new GraphicsBuffer[count];
            for (int i = 0; i < count; i++)
            {
                buffers[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, 4);
                buffers[i].SetData(new uint[] { 3, 5, 7, 11 });
            }
            return buffers;
        }

        [Test] public void MissingCallbacks_DrainReleasesBothSlotsExactlyOnce()
        {
            var buffers = Buffers(2);
            var first = buffers[0]; var second = buffers[1];
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var request = AsyncGPUReadback.Request(buffers[i]); // Deliberately no callback.
                    ClusterMeshReadbackRetirement.Retire(buffers, i, request);
                    ClusterMeshReadbackRetirement.Retire(buffers, i, request);
                }
                ClusterMeshReadbackRetirement.Drain();
                Assert.That(buffers, Is.All.Null);
                Assert.That(first.IsValid() || second.IsValid(), Is.False);
                ClusterMeshReadbackRetirement.Release(buffers, 0); // Late callback is safe.
                ClusterMeshReadbackRetirement.Release(buffers, 1);
                ClusterMeshReadbackRetirement.Drain();
            }
            finally { ClusterMeshReadbackRetirement.Drain(); first.Dispose(); second.Dispose(); }
        }

        [UnityTest] public IEnumerator NormalRetirement_CompletesWithoutWaiting()
        {
            var buffers = Buffers(1); var buffer = buffers[0]; bool completed = false;
            try
            {
                var request = AsyncGPUReadback.Request(buffer, r =>
                {
                    completed = true;
                    ClusterMeshReadbackRetirement.Release(buffers, 0);
                });
                int waits = ClusterMeshReadbackRetirement.WaitCount;
                ClusterMeshReadbackRetirement.Retire(buffers, 0, request);
                Assert.That(ClusterMeshReadbackRetirement.WaitCount, Is.EqualTo(waits));
                double end = EditorApplication.timeSinceStartup + 10;
                while (!completed && EditorApplication.timeSinceStartup < end) yield return null;
                Assert.That(completed, Is.True);
                Assert.That(buffers[0], Is.Null);
                Assert.That(buffer.IsValid(), Is.False);
                Assert.That(ClusterMeshReadbackRetirement.WaitCount, Is.EqualTo(waits));
            }
            finally { ClusterMeshReadbackRetirement.Drain(); buffer.Dispose(); }
        }

        [Test] public void ShutdownBeforeContextDisposal_DrainsLateRetirement()
        {
            ClusterMeshReadbackRetirement.BeginShutdown();
            var buffers = Buffers(1); var buffer = buffers[0];
            try
            {
                ClusterMeshReadbackRetirement.Retire(buffers, 0, AsyncGPUReadback.Request(buffer));
                Assert.That(buffers[0], Is.Null);
                Assert.That(buffer.IsValid(), Is.False);
                Assert.That(ClusterMeshReadbackRetirement.PendingCount, Is.Zero);
            }
            finally { buffer.Dispose(); }
        }

        [Test] public void RetirementQueue_IsBoundedWithMissingCallbacks()
        {
            var buffers = Buffers(ClusterMeshReadbackRetirement.Capacity + 8);
            try
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    ClusterMeshReadbackRetirement.Retire(buffers, i, AsyncGPUReadback.Request(buffers[i]));
                    Assert.That(ClusterMeshReadbackRetirement.PendingCount, Is.LessThanOrEqualTo(ClusterMeshReadbackRetirement.Capacity));
                }
                ClusterMeshReadbackRetirement.Drain();
                Assert.That(buffers, Is.All.Null);
            }
            finally { ClusterMeshReadbackRetirement.Drain(); foreach (var buffer in buffers) buffer?.Dispose(); }
        }

        [UnityTest] public IEnumerator RealStreamingContext_DisposeDuringReadbackDoesNotOrphanBuffers()
        {
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null) Assert.Ignore(unsupported);
            string folder = "Assets/ClusterMesh/Tests/__Readback" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/ClusterMesh/Tests", Path.GetFileName(folder));
            var mesh = ClusterMeshTestMeshes.Grid(8, 8);
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshDrawContext context = null;
            try
            {
                var settings = new ClusterMeshBakeSettings { enableStreaming = true };
                asset.CopyFrom(ClusterMeshBaker.Bake(mesh, new Material[1], settings), mesh, settings);
                string path = folder + "/Stream.asset";
                AssetDatabase.CreateAsset(asset, path);
                ClusterMeshStreamBaker.FinalizeStatic(asset, path, settings);
                context = new ClusterMeshDrawContext(asset,
                    AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute"), Shader.Find("ClusterMesh/Lit"));
                Assert.That(context.IsReady, Is.True, context.Error);
                var type = typeof(ClusterMeshDrawContext);
                var buffers = (GraphicsBuffer[])type.GetField("_streamRequestedPages", Hidden).GetValue(context);
                var first = buffers[0]; var second = buffers[1];
                type.GetField("_streamActiveRequestBuffer", Hidden).SetValue(context, 0);
                type.GetMethod("RequestStreamNodesFromGpu", Hidden).Invoke(context, null);
                context.Dispose();
                Assert.That(buffers[1], Is.Null, "Idle slot should release immediately");
                ClusterMeshReadbackRetirement.Drain();
                Assert.That(buffers, Is.All.Null);
                Assert.That(first.IsValid() || second.IsValid(), Is.False);
                context.Dispose();
                yield return null; // Exercise any callback arriving after the explicit drain.
                Assert.That(ClusterMeshReadbackRetirement.PendingCount, Is.Zero);
            }
            finally
            {
                context?.Dispose(); ClusterMeshReadbackRetirement.Drain(); ClusterMeshStreaming.Release(asset);
                Object.DestroyImmediate(mesh); AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
