using System.Collections.Generic;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>Separate registry so skinned work cannot alter static ClusterMesh batching.</summary>
    public static class ClusterSkinnedMeshSceneBatcher
    {
        static readonly List<ClusterSkinnedMeshRenderer> Renderers = new List<ClusterSkinnedMeshRenderer>();
        static readonly Dictionary<ClusterSkinnedMeshAsset, ClusterSkinnedMeshDrawContext> Contexts = new Dictionary<ClusterSkinnedMeshAsset, ClusterSkinnedMeshDrawContext>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        static readonly List<bool> CpuCull = new List<bool>(64);
        static readonly List<bool> CameraCull = new List<bool>(64);
        static readonly List<float> Times = new List<float>(64);
        static int _flushedFrame = int.MinValue;

        public static int CachedContextCount => Contexts.Count;

        public static void Register(ClusterSkinnedMeshRenderer renderer)
        {
            if (renderer != null && renderer.asset != null && !Renderers.Contains(renderer))
                Renderers.Add(renderer);
        }

        public static void Unregister(ClusterSkinnedMeshRenderer renderer)
        {
            Renderers.Remove(renderer);
            ClusterSkinnedMeshAsset asset = renderer != null ? renderer.asset : null;
            if (asset == null || Contexts.ContainsKey(asset) == false)
                return;
            for (int i = 0; i < Renderers.Count; i++)
                if (Renderers[i] != null && Renderers[i].asset == asset)
                    return;
            Contexts[asset].Dispose();
            Contexts.Remove(asset);
        }

        public static void Flush()
        {
            if (_flushedFrame == Time.frameCount)
                return;
            _flushedFrame = Time.frameCount;
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterSkinnedMeshRenderer seed = Renderers[i];
                if (seed == null || !seed.isActiveAndEnabled || seed.asset == null)
                    continue;
                Camera camera = seed.targetCamera != null ? seed.targetCamera : Camera.main;
                if (camera == null)
                    continue;
                // V1 submits one renderer at a time so clip/time/cull switches remain exact.
                Matrices.Clear(); CpuCull.Clear(); CameraCull.Clear(); Times.Clear();
                Add(seed, Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
                ClusterSkinnedMeshDrawContext ctx = GetOrCreate(seed);
                if (ctx == null) continue;
                ctx.EnableClusterColor = seed.showClusterColors;
                ctx.Draw(Matrices, CpuCull, CameraCull, Times, seed.clipIndex, seed.animationEvaluation,
                    seed.enableConeCull,
                    seed.lodErrorThreshold, camera, camera, seed.castShadows, seed.receiveShadows, seed.gameObject.layer);
            }
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
            return renderer == null ? null : new ClusterSkinnedMeshDrawContext(renderer.asset, renderer.cullShader, renderer.litShader);
        }

        static void Add(ClusterSkinnedMeshRenderer r, float clock)
        {
            Matrices.Add(r.transform.localToWorldMatrix);
            CpuCull.Add(r.enableCpuObjectCull);
            CameraCull.Add(r.enableCameraCull);
            Times.Add(r.CurrentNormalizedTime(clock));
        }

        static ClusterSkinnedMeshDrawContext GetOrCreate(ClusterSkinnedMeshRenderer r)
        {
            if (Contexts.TryGetValue(r.asset, out ClusterSkinnedMeshDrawContext ctx))
            {
                if (ctx != null && ctx.CanDraw)
                    return ctx;
                ctx?.Dispose();
                Contexts.Remove(r.asset);
            }
            ctx = new ClusterSkinnedMeshDrawContext(r.asset, r.cullShader, r.litShader);
            if (!ctx.IsReady) { ctx.Dispose(); return null; }
            Contexts.Add(r.asset, ctx);
            return ctx;
        }

        public static void DisposeCachedContexts()
        {
            foreach (var pair in Contexts) pair.Value.Dispose();
            Contexts.Clear();
            _flushedFrame = int.MinValue;
        }
    }
}
