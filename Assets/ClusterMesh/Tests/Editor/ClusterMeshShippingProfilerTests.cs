using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ClusterMesh.Tests
{
    public sealed class ClusterMeshShippingProfilerTests
    {
        const string ResultsPath = "D:/MiniGameV2/.superpowers/sdd/shipping-profiler-results.txt";

        [Test]
        public void TwoHundredSameAsset_KeptCountFollowsVisibleNotRegistered()
        {
            Light previousSun = RenderSettings.sun;
            var created = new List<UnityEngine.Object>();
            try
            {
                var camGo = new GameObject("CMShipProfCam");
                created.Add(camGo);
                var camera = camGo.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                camera.fieldOfView = 60f;
                camera.aspect = 16f / 9f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 80f;

                var sunGo = new GameObject("CMShipProfSun");
                created.Add(sunGo);
                var sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.LookRotation(Vector3.back);
                RenderSettings.sun = sun;

                var planes = new Plane[6];
                ClusterMeshFrustum.WorldPlanes(camera, planes);
                var receiver = new Plane[6];
                var shadow = new Plane[6];
                ClusterMeshObjectCull.BuildReceiverFrustumPlanes(
                    camera, ClusterMeshObjectCull.ShadowDistance(camera), receiver);
                ClusterMeshObjectCull.ExtrudePlanesToward(receiver, -sun.transform.forward, shadow);

                var imageA = BuildMatrices(20, 180);
                var imageB = BuildMatrices(10, 0);
                var localBounds = new Bounds(new Vector3(0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.01f));

                int keptAOn = CountKept(imageA, localBounds, planes, true, shadow);
                int keptAOff = CountKept(imageA, localBounds, planes, false, shadow);
                int keptBOn = CountKept(imageB, localBounds, planes, true, shadow);
                int keptBOff = CountKept(imageB, localBounds, planes, false, shadow);

                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 200; i++)
                    CountKept(imageA, localBounds, planes, true, shadow);
                sw.Stop();
                double cullMs = sw.Elapsed.TotalMilliseconds / 200.0;

                Assert.That(keptAOn, Is.EqualTo(20), "画像 A 剔开应只留镜头里 20");
                Assert.That(keptAOff, Is.EqualTo(200), "画像 A 剔关应进槽 200");
                Assert.That(keptBOn, Is.EqualTo(10));
                Assert.That(keptBOff, Is.EqualTo(10));

                int dispatchAOn = EstimateDispatch(200, keptAOn);
                int dispatchAOff = EstimateDispatch(200, keptAOff);
                int dispatchB = EstimateDispatch(10, keptBOn);

                var report = new StringBuilder();
                report.AppendLine("machine=" + SystemInfo.deviceName + " " + SystemInfo.processorType);
                report.AppendLine("date=2026-09-12");
                report.AppendLine("method=EditMode PrepareUrp + CopyCount GetData + ProfilerRecorder GC.Alloc");
                report.AppendLine("capability=" + (ClusterMeshCapability.GetUnsupportedReason() ?? "ok"));
                report.AppendLine("imageA_kept_on=" + keptAOn);
                report.AppendLine("imageA_kept_off=" + keptAOff);
                report.AppendLine("imageB_kept_on=" + keptBOn);
                report.AppendLine("imageB_kept_off=" + keptBOff);
                report.AppendLine("dispatch_est_A_on=" + dispatchAOn);
                report.AppendLine("dispatch_est_A_off=" + dispatchAOff);
                report.AppendLine("dispatch_est_B=" + dispatchB);
                report.AppendLine("cull_ms_A_on_avg=" + cullMs.ToString("0.000"));

                TryPrepareUrp(imageA, imageB, camera, created, report);

                Directory.CreateDirectory(Path.GetDirectoryName(ResultsPath));
                File.WriteAllText(ResultsPath, report.ToString());
                UnityEngine.Debug.Log("SHIPPING_PROFILER\n" + report);
            }
            finally
            {
                RenderSettings.sun = previousSun;
                for (int i = 0; i < created.Count; i++)
                {
                    if (created[i] != null)
                        UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }
        }

        static List<Matrix4x4> BuildMatrices(int inView, int behind)
        {
            var list = new List<Matrix4x4>(inView + behind);
            for (int i = 0; i < inView; i++)
            {
                float x = ((i + 0.5f) / inView - 0.5f) * 2f;
                list.Add(FacingCamera(new Vector3(x, 0f, 8f)));
            }

            for (int i = 0; i < behind; i++)
            {
                float x = ((i + 0.5f) / Mathf.Max(1, behind) - 0.5f) * 8f;
                list.Add(FacingCamera(new Vector3(x, 0f, -20f)));
            }

            return list;
        }

        static Matrix4x4 FacingCamera(Vector3 position)
        {
            return Matrix4x4.TRS(position, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
        }

        static int CountKept(
            IList<Matrix4x4> matrices,
            Bounds localBounds,
            Plane[] cameraPlanes,
            bool enableCull,
            Plane[] shadowPlanes)
        {
            int kept = 0;
            for (int i = 0; i < matrices.Count; i++)
            {
                Bounds world = ClusterMeshFrustum.TransformLocalBounds(localBounds, matrices[i]);
                if (ClusterMeshObjectCull.KeepObject(world, cameraPlanes, enableCull, true, shadowPlanes))
                    kept++;
            }

            return kept;
        }

        static int EstimateDispatch(int registered, int keptIfSingleChunk)
        {
            int chunkSize = ClusterMeshLimits.MaxBatchedObjects;
            int chunks = Mathf.CeilToInt(registered / (float)chunkSize);
            int dispatch = 0;
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                int start = chunk * chunkSize;
                int raw = Mathf.Min(chunkSize, registered - start);
                bool any = chunk == 0 ? keptIfSingleChunk > 0 : raw > 0;
                if (any)
                    dispatch++;
            }

            return dispatch;
        }

        static void TryPrepareUrp(
            List<Matrix4x4> imageA,
            List<Matrix4x4> imageB,
            Camera camera,
            List<UnityEngine.Object> created,
            StringBuilder report)
        {
            string unsupported = ClusterMeshCapability.GetUnsupportedReason();
            if (unsupported != null)
            {
                report.AppendLine("prepareUrp=ignored " + unsupported);
                report.AppendLine("gc_A_on=unmeasured");
                report.AppendLine("gc_A_off=unmeasured");
                report.AppendLine("gc_B=unmeasured");
                return;
            }

            var mesh = ClusterMeshTestMeshes.Triangle();
            created.Add(mesh);
            var bake = ClusterMeshBaker.Bake(mesh, new Material[1], new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            created.Add(asset);
            asset.CopyFrom(bake, mesh, new ClusterMeshBakeSettings { buildLodHierarchy = false });
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var cpuOn = Flags(imageA.Count, true);
            var cpuOff = Flags(imageA.Count, false);
            var cameraA = Flags(imageA.Count, true);
            var cpuB = Flags(imageB.Count, true);
            var cameraB = Flags(imageB.Count, true);

            using (var ctx = new ClusterMeshDrawContext(asset, cull, lit))
            {
                if (!ctx.IsReady)
                {
                    report.AppendLine("prepareUrp=ignored " + ctx.Error);
                    report.AppendLine("gc_A_on=unmeasured");
                    report.AppendLine("gc_A_off=unmeasured");
                    report.AppendLine("gc_B=unmeasured");
                    return;
                }

                report.AppendLine("clusters=" + asset.clusters.Length);
                MeasurePrepare(ctx, imageA, cpuOn, cameraA, camera, "A_on", report);
                MeasurePrepare(ctx, imageA, cpuOff, cameraA, camera, "A_off", report);
                MeasurePrepare(ctx, imageB, cpuB, cameraB, camera, "B", report);
            }
        }

        static bool[] Flags(int count, bool value)
        {
            var flags = new bool[count];
            for (int i = 0; i < count; i++)
                flags[i] = value;
            return flags;
        }

        static void MeasurePrepare(
            ClusterMeshDrawContext ctx,
            List<Matrix4x4> matrices,
            bool[] cpuCull,
            bool[] cameraCull,
            Camera camera,
            string label,
            StringBuilder report)
        {
            ctx.PrepareUrp(matrices, cpuCull, cameraCull, camera, true, true);
            ctx.PrepareUrp(matrices, cpuCull, cameraCull, camera, true, true);

            Profiler.enabled = true;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = Profiler.GetMonoUsedSizeLong();
            long gcAlloc;
            double ms;
            using (var rec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc"))
            {
                var sw = Stopwatch.StartNew();
                ctx.PrepareUrp(matrices, cpuCull, cameraCull, camera, true, true);
                sw.Stop();
                ms = sw.Elapsed.TotalMilliseconds;
                gcAlloc = SumRecorder(rec);
            }

            long after = Profiler.GetMonoUsedSizeLong();
            AsyncGPUReadback.WaitAllRequests();
            int chunks = ReadChunkCount(ctx, out int kept);
            ReadGpuCounts(ctx, out int mainInstances, out int shadowInstances);
            report.AppendLine("prepare_" + label + "_chunks=" + chunks);
            report.AppendLine("prepare_" + label + "_kept=" + kept);
            report.AppendLine("prepare_" + label + "_ms=" + ms.ToString("0.000"));
            report.AppendLine("main_" + label + "=" + mainInstances);
            report.AppendLine("shadow_" + label + "=" + shadowInstances);
            report.AppendLine("gc_" + label + "_alloc=" + gcAlloc);
            report.AppendLine("gc_" + label + "_monoDelta=" + (after - before));
        }

        static long SumRecorder(ProfilerRecorder rec)
        {
            if (!rec.Valid)
                return -1;
            long sum = 0;
            int count = rec.Count;
            for (int i = 0; i < count; i++)
                sum += rec.GetSample(i).Value;
            if (count == 0)
                return rec.LastValue;
            return sum;
        }

        static int ReadChunkCount(ClusterMeshDrawContext ctx, out int kept)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var list = (System.Collections.IList)typeof(ClusterMeshDrawContext)
                .GetField("_urpChunks", flags)
                .GetValue(ctx);
            kept = 0;
            if (list == null || list.Count == 0)
                return 0;

            FieldInfo nField = list[0].GetType().GetField("n");
            for (int i = 0; i < list.Count; i++)
                kept += (int)nField.GetValue(list[i]);
            return list.Count;
        }

        static void ReadGpuCounts(ClusterMeshDrawContext ctx, out int mainInstances, out int shadowInstances)
        {
            mainInstances = 0;
            shadowInstances = 0;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var list = (System.Collections.IList)typeof(ClusterMeshDrawContext)
                .GetField("_urpChunks", flags)
                .GetValue(ctx);
            if (list == null || list.Count == 0)
                return;

            Type chunkType = list[0].GetType();
            FieldInfo colorField = chunkType.GetField("colorArgs");
            FieldInfo shadowField = chunkType.GetField("shadowArgs");
            var args = new uint[5];
            for (int i = 0; i < list.Count; i++)
            {
                object chunk = list[i];
                SumArgs((GraphicsBuffer[])colorField.GetValue(chunk), args, ref mainInstances);
                SumArgs((GraphicsBuffer[])shadowField.GetValue(chunk), args, ref shadowInstances);
            }
        }

        static void SumArgs(GraphicsBuffer[] buffers, uint[] args, ref int total)
        {
            if (buffers == null)
                return;
            for (int i = 0; i < buffers.Length; i++)
            {
                if (buffers[i] == null)
                    continue;
                buffers[i].GetData(args);
                total += (int)args[1];
            }
        }
    }
}
