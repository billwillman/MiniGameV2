using System.Collections.Generic;
using UnityEngine;

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
        static readonly HashSet<int> Seen = new HashSet<int>();
        static int _flushedFrame = int.MinValue;
        static bool _loggedError;

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
                Matrices.Add(seed.transform.localToWorldMatrix);
                for (int j = i + 1; j < Renderers.Count; j++)
                {
                    ClusterMeshRenderer other = Renderers[j];
                    if (other == null || other.asset != seed.asset || ResolveCamera(other) != camera)
                        continue;
                    Seen.Add(j);
                    Matrices.Add(other.transform.localToWorldMatrix);
                }

                ClusterMeshDrawContext ctx = GetOrCreate(seed);
                if (ctx == null || !ctx.IsReady)
                    continue;
                ctx.Draw(Matrices, camera);
            }
        }

        public static int CountDrawCalls(int objectCount, int materialCount)
        {
            if (objectCount <= 0 || materialCount <= 0)
                return 0;
            int chunks = Mathf.CeilToInt(objectCount / (float)ClusterMeshLimits.MaxBatchedObjects);
            return chunks * materialCount;
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

        public static void ResetForTests()
        {
            Renderers.Clear();
            foreach (var kv in Contexts)
                kv.Value?.Dispose();
            Contexts.Clear();
            _flushedFrame = int.MinValue;
            _loggedError = false;
        }

        static Camera ResolveCamera(ClusterMeshRenderer renderer)
        {
            return renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
        }

        static ClusterMeshDrawContext GetOrCreate(ClusterMeshRenderer seed)
        {
            ClusterMeshAsset asset = seed.asset;
            if (Contexts.TryGetValue(asset, out ClusterMeshDrawContext existing))
                return existing;

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
