using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    /// <summary>Separate registry so skinned work cannot alter static ClusterMesh batching.</summary>
    public static class ClusterSkinnedMeshSceneBatcher
    {
        readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly ClusterSkinnedMeshAsset asset;
            public readonly Camera camera;
            public readonly ComputeShader cullShader;
            public readonly Shader litShader;
            public readonly int assetId;
            public readonly int cameraId;
            public readonly int cullShaderId;
            public readonly int litShaderId;
            public readonly int clipIndex;
            public readonly int layer;
            public readonly ClusterSkinnedAnimationEvaluation animationEvaluation;
            public readonly bool enableParallelBonePrefix;
            public readonly bool enableConeCull;
            public readonly bool showClusterColors;
            public readonly bool castShadows;
            public readonly bool receiveShadows;
            public readonly float lodErrorThreshold;

            public BatchKey(ClusterSkinnedMeshRenderer renderer, Camera resolvedCamera)
            {
                asset = renderer.asset;
                camera = resolvedCamera;
                cullShader = renderer.cullShader;
                litShader = renderer.litShader;
                assetId = asset.GetInstanceID();
                cameraId = camera.GetInstanceID();
                cullShaderId = cullShader.GetInstanceID();
                litShaderId = litShader.GetInstanceID();
                clipIndex = renderer.clipIndex;
                layer = renderer.gameObject.layer;
                animationEvaluation = renderer.animationEvaluation;
                enableParallelBonePrefix = renderer.animationEvaluation == ClusterSkinnedAnimationEvaluation.CpuCurves &&
                    renderer.enableParallelBonePrefix;
                enableConeCull = renderer.enableConeCull;
                showClusterColors = renderer.showClusterColors;
                castShadows = renderer.castShadows;
                receiveShadows = renderer.receiveShadows;
                lodErrorThreshold = renderer.lodErrorThreshold;
            }

            public bool Equals(BatchKey other)
            {
                return assetId == other.assetId && cameraId == other.cameraId &&
                    cullShaderId == other.cullShaderId && litShaderId == other.litShaderId &&
                    clipIndex == other.clipIndex && layer == other.layer &&
                    animationEvaluation == other.animationEvaluation &&
                    enableParallelBonePrefix == other.enableParallelBonePrefix &&
                    enableConeCull == other.enableConeCull &&
                    showClusterColors == other.showClusterColors &&
                    castShadows == other.castShadows && receiveShadows == other.receiveShadows &&
                    lodErrorThreshold.Equals(other.lodErrorThreshold);
            }

            public override bool Equals(object obj) => obj is BatchKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = assetId;
                    hash = (hash * 397) ^ cameraId;
                    hash = (hash * 397) ^ cullShaderId;
                    hash = (hash * 397) ^ litShaderId;
                    hash = (hash * 397) ^ clipIndex;
                    hash = (hash * 397) ^ layer;
                    hash = (hash * 397) ^ (int)animationEvaluation;
                    hash = (hash * 397) ^ enableParallelBonePrefix.GetHashCode();
                    hash = (hash * 397) ^ enableConeCull.GetHashCode();
                    hash = (hash * 397) ^ showClusterColors.GetHashCode();
                    hash = (hash * 397) ^ castShadows.GetHashCode();
                    hash = (hash * 397) ^ receiveShadows.GetHashCode();
                    hash = (hash * 397) ^ lodErrorThreshold.GetHashCode();
                    return hash;
                }
            }
        }

        readonly struct ContextKey : IEquatable<ContextKey>
        {
            public readonly BatchKey batch;
            public readonly int batchSlot;

            public ContextKey(BatchKey batch, int batchSlot)
            {
                this.batch = batch;
                this.batchSlot = batchSlot;
            }

            public bool Equals(ContextKey other) => batchSlot == other.batchSlot && batch.Equals(other.batch);
            public override bool Equals(object obj) => obj is ContextKey other && Equals(other);
            public override int GetHashCode() => unchecked((batch.GetHashCode() * 397) ^ batchSlot);
        }

        static readonly List<ClusterSkinnedMeshRenderer> Renderers = new List<ClusterSkinnedMeshRenderer>();
        static readonly Dictionary<ContextKey, ClusterSkinnedMeshDrawContext> Contexts =
            new Dictionary<ContextKey, ClusterSkinnedMeshDrawContext>();
        static readonly HashSet<int> SeenRendererIds = new HashSet<int>();
        static readonly HashSet<ContextKey> UsedContexts = new HashSet<ContextKey>();
        static readonly List<ContextKey> StaleContexts = new List<ContextKey>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<Matrix4x4> PreviousMatrices = new List<Matrix4x4>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<bool> CpuCull = new List<bool>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<bool> CameraCull = new List<bool>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<float> Times = new List<float>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<float> PreviousTimes = new List<float>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<bool> MotionVectorFlags = new List<bool>(ClusterMeshLimits.MaxBatchedObjects);
        static readonly List<ClusterSkinnedMeshDrawContext> UrpPrepared = new List<ClusterSkinnedMeshDrawContext>();
        static int _flushedFrame = int.MinValue;
        static int _urpPreparedFrame = int.MinValue;
        static int _urpPreparedCameraId;

        public static int LegacyFlushBatchCountForTests { get; private set; }
        public static int UrpPreparedCountForTests => UrpPrepared.Count;
        public static int UrpShadowSubmitCountForTests { get; private set; }
        public static int CachedContextCount => Contexts.Count;
        public static int RegisteredCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Renderers.Count; i++)
                    if (Renderers[i] != null)
                        count++;
                return count;
            }
        }

        public static void Register(ClusterSkinnedMeshRenderer renderer)
        {
            if (renderer != null && renderer.asset != null && !Renderers.Contains(renderer))
                Renderers.Add(renderer);
        }

        public static void Unregister(ClusterSkinnedMeshRenderer renderer)
        {
            Renderers.Remove(renderer);
            ClusterSkinnedMeshAsset asset = renderer != null ? renderer.asset : null;
            if (asset == null)
                return;
            for (int i = 0; i < Renderers.Count; i++)
                if (Renderers[i] != null && Renderers[i].asset == asset)
                    return;

            StaleContexts.Clear();
            foreach (KeyValuePair<ContextKey, ClusterSkinnedMeshDrawContext> pair in Contexts)
                if (pair.Key.batch.assetId == asset.GetInstanceID())
                    StaleContexts.Add(pair.Key);
            DisposeStaleContexts();
        }

        public static void Flush()
        {
            if (_flushedFrame == Time.frameCount)
                return;
            _flushedFrame = Time.frameCount;
            UsedContexts.Clear();
            ForEachRegisteredBatch((seed, batch, matrices, previousMatrices, cpuCull, cameraCull, times, previousTimes, motionVectorFlags, batchSlot) =>
            {
                var contextKey = new ContextKey(batch, batchSlot);
                UsedContexts.Add(contextKey);
                if (ClusterMeshUrpBridge.ShouldSkipLegacyFlush(batch.camera))
                {
                    GetOrCreate(contextKey);
                    return;
                }

                LegacyFlushBatchCountForTests++;
                ClusterSkinnedMeshDrawContext context = GetOrCreate(contextKey);
                if (context == null)
                    return;
                context.EnableClusterColor = batch.showClusterColors;
                context.DrawMotion(matrices, previousMatrices, cpuCull, cameraCull, times, previousTimes,
                    motionVectorFlags, batch.clipIndex, batch.animationEvaluation,
                    batch.enableParallelBonePrefix, batch.enableConeCull, batch.lodErrorThreshold,
                    batch.camera, batch.camera,
                    batch.castShadows, batch.receiveShadows, batch.layer);
            });
            DisposeUnusedContexts();
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

            ForEachRegisteredBatch((seed, batch, matrices, previousMatrices, cpuCull, cameraCull, times, previousTimes, motionVectorFlags, batchSlot) =>
            {
                if (batch.camera != camera)
                    return;
                ClusterSkinnedMeshDrawContext context = GetOrCreate(new ContextKey(batch, batchSlot));
                if (context == null)
                    return;
                context.EnableClusterColor = batch.showClusterColors;
                if (context.PrepareUrpMotion(matrices, previousMatrices, cpuCull, cameraCull, times, previousTimes,
                    motionVectorFlags, batch.clipIndex, batch.animationEvaluation,
                    batch.enableParallelBonePrefix, batch.enableConeCull, batch.lodErrorThreshold,
                    camera, batch.castShadows, batch.receiveShadows, batch.layer))
                    UrpPrepared.Add(context);
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
                ClusterSkinnedMeshRenderer renderer = Renderers[i];
                Camera resolved = renderer != null
                    ? (renderer.targetCamera != null ? renderer.targetCamera : Camera.main)
                    : null;
                if (renderer != null && renderer.isActiveAndEnabled &&
                    renderer.enableMotionVectors && resolved == camera)
                    return true;
            }
            return false;
        }

        public static void ResetForTests()
        {
            Renderers.Clear();
            UrpPrepared.Clear();
            LegacyFlushBatchCountForTests = 0;
            UrpShadowSubmitCountForTests = 0;
            _urpPreparedFrame = int.MinValue;
            _urpPreparedCameraId = 0;
            DisposeCachedContexts();
        }

        public static void CollectRegisteredForEditor(List<ClusterSkinnedMeshRenderer> output)
        {
            output.Clear();
            for (int i = 0; i < Renderers.Count; i++)
                if (Renderers[i] != null && Renderers[i].isActiveAndEnabled)
                    output.Add(Renderers[i]);
        }

        public static ClusterSkinnedMeshDrawContext CreatePreviewContext(ClusterSkinnedMeshRenderer renderer)
        {
            return renderer == null ? null :
                new ClusterSkinnedMeshDrawContext(renderer.asset, renderer.cullShader, renderer.litShader);
        }

        delegate void BatchCallback(
            ClusterSkinnedMeshRenderer seed,
            BatchKey batch,
            List<Matrix4x4> matrices,
            List<Matrix4x4> previousMatrices,
            List<bool> cpuCull,
            List<bool> cameraCull,
            List<float> times,
            List<float> previousTimes,
            List<bool> motionVectorFlags,
            int batchSlot);

        static void ForEachRegisteredBatch(BatchCallback callback)
        {
            for (int i = Renderers.Count - 1; i >= 0; i--)
            {
                if (Renderers[i] == null)
                    Renderers.RemoveAt(i);
            }

            SeenRendererIds.Clear();
            float clock = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterSkinnedMeshRenderer seed = Renderers[i];
                if (seed == null || SeenRendererIds.Contains(seed.GetInstanceID()) ||
                    !TryGetBatchKey(seed, out BatchKey batch))
                    continue;

                int batchSlot = 0;
                while (true)
                {
                    Matrices.Clear();
                    PreviousMatrices.Clear();
                    CpuCull.Clear();
                    CameraCull.Clear();
                    Times.Clear();
                    PreviousTimes.Clear();
                    MotionVectorFlags.Clear();
                    for (int j = i; j < Renderers.Count && Matrices.Count < ClusterMeshLimits.MaxBatchedObjects; j++)
                    {
                        ClusterSkinnedMeshRenderer candidate = Renderers[j];
                        if (candidate == null || SeenRendererIds.Contains(candidate.GetInstanceID()) ||
                            !TryGetBatchKey(candidate, out BatchKey candidateBatch) || !batch.Equals(candidateBatch))
                            continue;
                        SeenRendererIds.Add(candidate.GetInstanceID());
                        Add(candidate, clock);
                    }
                    if (Matrices.Count == 0)
                        break;
                    callback(seed, batch, Matrices, PreviousMatrices, CpuCull, CameraCull,
                        Times, PreviousTimes, MotionVectorFlags, batchSlot++);
                }
            }
        }

        static bool TryGetBatchKey(ClusterSkinnedMeshRenderer renderer, out BatchKey key)
        {
            key = default;
            if (renderer == null || !renderer.isActiveAndEnabled || renderer.asset == null ||
                renderer.cullShader == null || renderer.litShader == null)
                return false;
            Camera camera = renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
            if (camera == null)
                return false;
            key = new BatchKey(renderer, camera);
            return true;
        }

        static void Add(ClusterSkinnedMeshRenderer renderer, float clock)
        {
            Matrix4x4 currentMatrix = renderer.transform.localToWorldMatrix;
            float currentTime = renderer.CurrentNormalizedTime(clock);
            renderer.CapturePreviousMotion(currentMatrix, currentTime, renderer.clipIndex, Time.frameCount,
                out Matrix4x4 previousMatrix, out float previousTime);
            Matrices.Add(currentMatrix);
            PreviousMatrices.Add(previousMatrix);
            CpuCull.Add(renderer.enableCpuObjectCull);
            CameraCull.Add(renderer.enableCameraCull);
            Times.Add(currentTime);
            PreviousTimes.Add(previousTime);
            MotionVectorFlags.Add(renderer.enableMotionVectors);
        }

        static ClusterSkinnedMeshDrawContext GetOrCreate(ContextKey key)
        {
            if (Contexts.TryGetValue(key, out ClusterSkinnedMeshDrawContext context))
            {
                if (context != null && context.CanDraw)
                    return context;
                context?.Dispose();
                Contexts.Remove(key);
            }
            context = new ClusterSkinnedMeshDrawContext(
                key.batch.asset, key.batch.cullShader, key.batch.litShader);
            if (!context.IsReady)
            {
                context.Dispose();
                return null;
            }
            Contexts.Add(key, context);
            return context;
        }

        static void DisposeUnusedContexts()
        {
            StaleContexts.Clear();
            foreach (KeyValuePair<ContextKey, ClusterSkinnedMeshDrawContext> pair in Contexts)
                if (!UsedContexts.Contains(pair.Key))
                    StaleContexts.Add(pair.Key);
            DisposeStaleContexts();
        }

        static void DisposeStaleContexts()
        {
            for (int i = 0; i < StaleContexts.Count; i++)
            {
                ContextKey key = StaleContexts[i];
                Contexts[key]?.Dispose();
                Contexts.Remove(key);
            }
            StaleContexts.Clear();
        }

        public static void DisposeCachedContexts()
        {
            UrpPrepared.Clear();
            foreach (KeyValuePair<ContextKey, ClusterSkinnedMeshDrawContext> pair in Contexts)
                pair.Value?.Dispose();
            Contexts.Clear();
            SeenRendererIds.Clear();
            UsedContexts.Clear();
            StaleContexts.Clear();
            _flushedFrame = int.MinValue;
        }
    }
}
