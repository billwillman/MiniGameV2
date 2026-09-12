using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public readonly struct ClusterMeshBatchDesc
    {
        public readonly ClusterMeshAsset asset;
        public readonly Camera camera;
        public readonly int objectCount;
        public readonly int materialCount;
        public readonly int drawCallCount;

        public ClusterMeshBatchDesc(ClusterMeshAsset asset, Camera camera, int objectCount, int materialCount, int drawCallCount)
        {
            this.asset = asset;
            this.camera = camera;
            this.objectCount = objectCount;
            this.materialCount = materialCount;
            this.drawCallCount = drawCallCount;
        }
    }

    public static class ClusterMeshSceneBatcher
    {
        static readonly List<ClusterMeshRenderer> Renderers = new List<ClusterMeshRenderer>();
        static readonly Dictionary<ClusterMeshAsset, ClusterMeshDrawContext> Contexts = new Dictionary<ClusterMeshAsset, ClusterMeshDrawContext>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        static readonly List<Matrix4x4> PreviousMatrices = new List<Matrix4x4>(64);
        static readonly List<bool> MotionVectorFlags = new List<bool>(64);
        static readonly List<bool> CpuCullFlags = new List<bool>(64);
        static readonly List<bool> CameraCullFlags = new List<bool>(64);
        static readonly List<bool> LightProbeFlags = new List<bool>(64);
        static readonly List<bool> AmbientSkyFlags = new List<bool>(64);
        static readonly List<bool> FogFlags = new List<bool>(64);
        static readonly HashSet<int> Seen = new HashSet<int>();
        static readonly List<ClusterMeshDrawContext> UrpPrepared = new List<ClusterMeshDrawContext>();
        static int _flushedFrame = int.MinValue;
        static int _urpPreparedFrame = int.MinValue;
        static int _urpPreparedCameraId;
        static bool _loggedError;
        public static int UrpPreparedCountForTests => UrpPrepared.Count;
        public static int UrpShadowSubmitCountForTests { get; private set; }

        delegate void BatchCallback(
            ClusterMeshRenderer seed,
            Camera camera,
            List<Matrix4x4> matrices,
            List<Matrix4x4> previousMatrices,
            List<bool> motionVectorFlags,
            List<bool> cpuCullFlags,
            List<bool> cameraCullFlags,
            List<bool> lightProbeFlags,
            List<bool> ambientSkyFlags,
            List<bool> fogFlags,
            bool clusterColors,
            float lodErrorThreshold,
            bool batchCast);

        public static int RegisteredCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Renderers.Count; i++)
                {
                    if (Renderers[i] != null)
                        n++;
                }

                return n;
            }
        }

        public static void Register(ClusterMeshRenderer renderer)
        {
            if (renderer == null || renderer.asset == null || renderer.asset.clusters == null || renderer.asset.clusters.Length == 0)
                return;
            if (!Renderers.Contains(renderer))
                Renderers.Add(renderer);
            GetOrCreate(renderer);
        }

        public static void Unregister(ClusterMeshRenderer renderer)
        {
            ClusterMeshAsset asset = renderer != null ? renderer.asset : null;
            Renderers.Remove(renderer);
            if (asset == null)
                return;

            for (int i = 0; i < Renderers.Count; i++)
            {
                if (Renderers[i] != null && Renderers[i].asset == asset)
                    return;
            }

            if (Contexts.TryGetValue(asset, out ClusterMeshDrawContext ctx))
            {
                ctx.Dispose();
                Contexts.Remove(asset);
            }
        }

        public static void Flush()
        {
            if (Time.frameCount == _flushedFrame)
                return;
            _flushedFrame = Time.frameCount;

            ForEachRegisteredBatch((seed, camera, matrices, previousMatrices, motionVectorFlags, cpuCullFlags, cameraCullFlags, lightProbeFlags, ambientSkyFlags, fogFlags, clusterColors, lodT, batchCast) =>
            {
                if (ClusterMeshUrpBridge.ShouldSkipLegacyFlush(camera))
                    return;
                ClusterMeshDrawContext ctx = GetOrCreate(seed);
                if (ctx == null || !ctx.CanDraw)
                    return;
                ctx.EnableConeCull = seed.enableConeCull;
                ctx.EnableClusterColor = clusterColors;
                ctx.LodErrorThreshold = lodT;
                ctx.DrawMotion(matrices, previousMatrices, motionVectorFlags,
                    cpuCullFlags, cameraCullFlags, camera, batchCast, seed.receiveShadows,
                    lightProbeFlags, ambientSkyFlags, fogFlags);
            });
        }

        public static void PrepareAndSubmitUrpShadows(Camera camera)
        {
            if (!ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;
            int cameraId = camera.GetInstanceID();
            if (_urpPreparedFrame == Time.frameCount && _urpPreparedCameraId == cameraId)
                return;

            UrpPrepared.Clear();
            _urpPreparedFrame = Time.frameCount;
            _urpPreparedCameraId = cameraId;
            UrpShadowSubmitCountForTests++;

            ForEachRegisteredBatch((seed, resolved, matrices, previousMatrices, motionVectorFlags, cpuCullFlags, cameraCullFlags, lightProbeFlags, ambientSkyFlags, fogFlags, clusterColors, lodT, batchCast) =>
            {
                if (resolved != camera)
                    return;
                ClusterMeshDrawContext ctx = GetOrCreate(seed);
                if (ctx == null || !ctx.CanDraw)
                    return;
                ctx.EnableConeCull = seed.enableConeCull;
                ctx.EnableClusterColor = clusterColors;
                ctx.LodErrorThreshold = lodT;
                if (ctx.PrepareUrpMotion(matrices, previousMatrices, motionVectorFlags,
                        cpuCullFlags, cameraCullFlags, camera, batchCast, seed.receiveShadows,
                        lightProbeFlags, ambientSkyFlags, fogFlags))
                    UrpPrepared.Add(ctx);
            });
        }

        public static void SubmitUrpDepth(Camera camera, CommandBuffer cmd)
        {
            if (cmd == null || !ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;
            for (int i = 0; i < UrpPrepared.Count; i++)
                UrpPrepared[i].SubmitUrpDepth(cmd);
        }

        public static void SubmitUrpColor(Camera camera, CommandBuffer cmd)
        {
            if (cmd == null || !ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;
            for (int i = 0; i < UrpPrepared.Count; i++)
                UrpPrepared[i].SubmitUrpColor(cmd);
        }

        public static void SubmitUrpGBuffer(Camera camera, CommandBuffer cmd)
        {
            if (cmd == null || !ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;
            for (int i = 0; i < UrpPrepared.Count; i++)
                UrpPrepared[i].SubmitUrpGBuffer(cmd);
        }

        public static void SubmitUrpMotionVectors(Camera camera, CommandBuffer cmd)
        {
            if (cmd == null || !ClusterMeshUrpBridge.ShouldSubmitUrp(camera))
                return;
            for (int i = 0; i < UrpPrepared.Count; i++)
                UrpPrepared[i].SubmitUrpMotionVectors(cmd);
        }

        public static bool HasMotionVectors(Camera camera)
        {
            if (camera == null)
                return false;
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterMeshRenderer renderer = Renderers[i];
                if (renderer != null && renderer.isActiveAndEnabled &&
                    renderer.enableMotionVectors && ResolveCamera(renderer) == camera)
                    return true;
            }
            return false;
        }

        public static int CountDrawCalls(int objectCount, int materialCount)
        {
            if (objectCount <= 0 || materialCount <= 0)
                return 0;
            int chunks = Mathf.CeilToInt(objectCount / (float)ClusterMeshLimits.MaxBatchedObjects);
            return chunks * materialCount;
        }

        public static int CountIndirectDraws(int objectCount, int materialCount, bool splitShadows)
        {
            int color = CountDrawCalls(objectCount, materialCount);
            if (color <= 0)
                return 0;
            return splitShadows ? color * 2 : color;
        }

        public static void CollectBatches(IList<ClusterMeshRenderer> source, List<ClusterMeshBatchDesc> dest)
        {
            dest.Clear();
            if (source == null)
                return;

            var seen = new HashSet<int>();
            for (int i = 0; i < source.Count; i++)
            {
                if (!seen.Add(i))
                    continue;
                ClusterMeshRenderer seed = source[i];
                if (seed == null || seed.asset == null)
                    continue;
                Camera camera = ResolveCamera(seed);
                if (camera == null)
                    continue;

                int count = 1;
                for (int j = i + 1; j < source.Count; j++)
                {
                    ClusterMeshRenderer other = source[j];
                    if (other == null || other.asset != seed.asset || ResolveCamera(other) != camera)
                        continue;
                    seen.Add(j);
                    count++;
                }

                int materials = seed.asset.materials != null && seed.asset.materials.Length > 0
                    ? seed.asset.materials.Length
                    : 1;
                dest.Add(new ClusterMeshBatchDesc(seed.asset, camera, count, materials, CountDrawCalls(count, materials)));
            }
        }

        public static void CollectRegisteredBatches(List<ClusterMeshBatchDesc> dest)
        {
            CollectBatches(Renderers, dest);
        }

#if UNITY_EDITOR
        public static void CollectRegisteredRenderersForEditor(List<ClusterMeshRenderer> dest)
        {
            dest.Clear();
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterMeshRenderer renderer = Renderers[i];
                if (renderer != null && renderer.isActiveAndEnabled)
                    dest.Add(renderer);
            }
        }
#endif

        public static void DisposeCachedContexts()
        {
            foreach (var kv in Contexts)
                kv.Value?.Dispose();
            Contexts.Clear();
            _flushedFrame = int.MinValue;
        }

        public static void ResetForTests()
        {
            Renderers.Clear();
            UrpPrepared.Clear();
            DisposeCachedContexts();
            _loggedError = false;
            _urpPreparedFrame = int.MinValue;
            _urpPreparedCameraId = 0;
            UrpShadowSubmitCountForTests = 0;
        }

        static void ForEachRegisteredBatch(BatchCallback callback)
        {
            for (int i = Renderers.Count - 1; i >= 0; i--)
            {
                if (Renderers[i] == null)
                    Renderers.RemoveAt(i);
            }

            Seen.Clear();
            for (int i = 0; i < Renderers.Count; i++)
            {
                if (!Seen.Add(i))
                    continue;

                ClusterMeshRenderer seed = Renderers[i];
                Camera camera = ResolveCamera(seed);
                if (camera == null || seed.asset == null)
                    continue;

                Matrices.Clear();
                PreviousMatrices.Clear();
                MotionVectorFlags.Clear();
                CpuCullFlags.Clear();
                CameraCullFlags.Clear();
                LightProbeFlags.Clear();
                AmbientSkyFlags.Clear();
                FogFlags.Clear();
                Matrix4x4 seedMatrix = seed.transform.localToWorldMatrix;
                Matrices.Add(seedMatrix);
                PreviousMatrices.Add(seed.CapturePreviousMotionMatrix(seedMatrix, Time.frameCount));
                MotionVectorFlags.Add(seed.enableMotionVectors);
                CpuCullFlags.Add(seed.enableCpuObjectCull);
                CameraCullFlags.Add(seed.enableCameraCull);
                LightProbeFlags.Add(seed.enableLightProbes);
                AmbientSkyFlags.Add(seed.enableAmbientSky);
                FogFlags.Add(seed.enableFog);
                bool clusterColors = seed.showClusterColors;
                float lodT = seed.lodErrorThreshold;
                bool batchCast = seed.castShadows;
                for (int j = i + 1; j < Renderers.Count; j++)
                {
                    ClusterMeshRenderer other = Renderers[j];
                    if (other == null || other.asset != seed.asset || ResolveCamera(other) != camera)
                        continue;
                    Seen.Add(j);
                    Matrix4x4 otherMatrix = other.transform.localToWorldMatrix;
                    Matrices.Add(otherMatrix);
                    PreviousMatrices.Add(other.CapturePreviousMotionMatrix(otherMatrix, Time.frameCount));
                    MotionVectorFlags.Add(other.enableMotionVectors);
                    CpuCullFlags.Add(other.enableCpuObjectCull);
                    CameraCullFlags.Add(other.enableCameraCull);
                    LightProbeFlags.Add(other.enableLightProbes);
                    AmbientSkyFlags.Add(other.enableAmbientSky);
                    FogFlags.Add(other.enableFog);
                    clusterColors |= other.showClusterColors;
                    lodT = Mathf.Max(lodT, other.lodErrorThreshold);
                    batchCast |= other.castShadows;
                }

                callback(seed, camera, Matrices, PreviousMatrices, MotionVectorFlags,
                    CpuCullFlags, CameraCullFlags, LightProbeFlags, AmbientSkyFlags, FogFlags,
                    clusterColors, lodT, batchCast);
            }
        }

        static Camera ResolveCamera(ClusterMeshRenderer renderer)
        {
            return renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
        }

        static ClusterMeshDrawContext GetOrCreate(ClusterMeshRenderer seed)
        {
            ClusterMeshAsset asset = seed.asset;
            if (Contexts.TryGetValue(asset, out ClusterMeshDrawContext existing))
            {
                if (existing != null && existing.CanDraw)
                    return existing;
                existing?.Dispose();
                Contexts.Remove(asset);
            }

            var ctx = new ClusterMeshDrawContext(asset, seed.cullShader, seed.litShader);
            if (!ctx.IsReady)
            {
                if (!_loggedError && !string.IsNullOrEmpty(ctx.Error))
                {
                    Debug.LogError("ClusterMesh: " + ctx.Error);
                    _loggedError = true;
                }

                ctx.Dispose();
                return null;
            }

            Contexts[asset] = ctx;
            return ctx;
        }
    }
}
