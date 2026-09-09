using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>
    /// Submits ClusterMesh draws for Scene view cameras while the editor is not playing.
    /// Kept in the Editor assembly so player rendering and builds are unaffected.
    /// </summary>
    public static class ClusterMeshSceneViewRenderer
    {
        readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly ClusterMeshAsset asset;
            public readonly Camera camera;
            public readonly ComputeShader cullShader;
            public readonly Shader litShader;
            public readonly int assetId;
            public readonly int cameraId;
            public readonly int cullShaderId;
            public readonly int litShaderId;
            public readonly int layer;
            public readonly bool enableConeCull;
            public readonly bool showClusterColors;
            public readonly float lodErrorThreshold;
            public readonly bool castShadows;
            public readonly bool receiveShadows;

            public BatchKey(ClusterMeshRenderer renderer, Camera sceneCamera, ComputeShader cull, Shader lit)
            {
                asset = renderer.asset;
                camera = sceneCamera;
                cullShader = cull;
                litShader = lit;
                assetId = asset.GetInstanceID();
                cameraId = camera.GetInstanceID();
                cullShaderId = cullShader.GetInstanceID();
                litShaderId = litShader.GetInstanceID();
                layer = renderer.gameObject.layer;
                enableConeCull = renderer.enableConeCull;
                showClusterColors = renderer.showClusterColors;
                lodErrorThreshold = renderer.lodErrorThreshold;
                castShadows = renderer.castShadows;
                receiveShadows = renderer.receiveShadows;
            }

            public bool Equals(BatchKey other)
            {
                return assetId == other.assetId
                    && cameraId == other.cameraId
                    && cullShaderId == other.cullShaderId
                    && litShaderId == other.litShaderId
                    && layer == other.layer
                    && enableConeCull == other.enableConeCull
                    && showClusterColors == other.showClusterColors
                    && lodErrorThreshold.Equals(other.lodErrorThreshold)
                    && castShadows == other.castShadows
                    && receiveShadows == other.receiveShadows;
            }

            public override bool Equals(object obj)
            {
                return obj is BatchKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = assetId;
                    hash = (hash * 397) ^ cameraId;
                    hash = (hash * 397) ^ cullShaderId;
                    hash = (hash * 397) ^ litShaderId;
                    hash = (hash * 397) ^ layer;
                    hash = (hash * 397) ^ enableConeCull.GetHashCode();
                    hash = (hash * 397) ^ showClusterColors.GetHashCode();
                    hash = (hash * 397) ^ lodErrorThreshold.GetHashCode();
                    hash = (hash * 397) ^ castShadows.GetHashCode();
                    hash = (hash * 397) ^ receiveShadows.GetHashCode();
                    return hash;
                }
            }
        }

        static readonly Dictionary<BatchKey, ClusterMeshDrawContext> Contexts =
            new Dictionary<BatchKey, ClusterMeshDrawContext>();
        static readonly HashSet<BatchKey> UsedContexts = new HashSet<BatchKey>();
        static readonly HashSet<int> SeenRenderers = new HashSet<int>();
        static readonly List<BatchKey> StaleContexts = new List<BatchKey>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        static readonly List<bool> CpuCullFlags = new List<bool>(64);

        static readonly List<ClusterMeshRenderer> Renderers = new List<ClusterMeshRenderer>(64);

        public static int CachedContextCount => Contexts.Count;

        public static bool IsLayerVisible(int layer, int editorVisibleLayers, int cameraCullingMask)
        {
            int layerMask = 1 << layer;
            return (editorVisibleLayers & layerMask) != 0
                && (cameraCullingMask & layerMask) != 0;
        }

        public static void DrawAllSceneViews()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ClusterMeshSceneBatcher.CollectRegisteredRenderersForEditor(Renderers);
            UsedContexts.Clear();
            StageHandle stage = StageUtility.GetCurrentStageHandle();
            IList sceneViews = SceneView.sceneViews;
            for (int i = 0; i < sceneViews.Count; i++)
            {
                var sceneView = sceneViews[i] as SceneView;
                Camera camera = sceneView != null ? sceneView.camera : null;
                if (camera != null && camera.cameraType == CameraType.SceneView)
                    DrawCamera(camera, stage);
            }

            DisposeUnusedContexts();
            if (Renderers.Count > 0)
                SceneView.RepaintAll();
        }

        public static void DisposeCachedContexts()
        {
            foreach (var pair in Contexts)
                pair.Value?.Dispose();
            Contexts.Clear();
            UsedContexts.Clear();
            StaleContexts.Clear();
            SeenRenderers.Clear();
            Matrices.Clear();
            CpuCullFlags.Clear();
            Renderers.Clear();
        }

        static void DrawCamera(Camera camera, StageHandle stage)
        {
            SeenRenderers.Clear();
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterMeshRenderer seed = Renderers[i];
                if (seed == null || SeenRenderers.Contains(seed.GetInstanceID())
                    || !TryGetBatchKey(seed, camera, stage, out BatchKey key))
                    continue;

                SeenRenderers.Add(seed.GetInstanceID());
                Matrices.Clear();
                CpuCullFlags.Clear();
                AddRenderer(seed);

                for (int j = i + 1; j < Renderers.Count; j++)
                {
                    ClusterMeshRenderer other = Renderers[j];
                    if (other == null || SeenRenderers.Contains(other.GetInstanceID())
                        || !TryGetBatchKey(other, camera, stage, out BatchKey otherKey)
                        || !key.Equals(otherKey))
                        continue;

                    SeenRenderers.Add(other.GetInstanceID());
                    AddRenderer(other);
                }

                UsedContexts.Add(key);
                ClusterMeshDrawContext context = GetOrCreateContext(key);
                if (context == null || !context.IsReady)
                    continue;

                context.EnableConeCull = key.enableConeCull;
                context.EnableClusterColor = key.showClusterColors;
                context.LodErrorThreshold = key.lodErrorThreshold;
                context.EditorDrawLayer = key.layer;
                context.Draw(
                    Matrices, CpuCullFlags, camera,
                    key.castShadows, key.receiveShadows);
            }
        }

        static bool TryGetBatchKey(
            ClusterMeshRenderer renderer,
            Camera camera,
            StageHandle stage,
            out BatchKey key)
        {
            key = default;
            if (!IsSceneVisible(renderer, camera, stage))
                return false;

            ComputeShader cull = renderer.cullShader;
            if (cull == null)
                cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            Shader lit = renderer.litShader != null ? renderer.litShader : Shader.Find("ClusterMesh/Lit");
            if (cull == null || lit == null)
                return false;

            key = new BatchKey(renderer, camera, cull, lit);
            return true;
        }

        static bool IsSceneVisible(ClusterMeshRenderer renderer, Camera camera, StageHandle stage)
        {
            if (renderer == null || !renderer.isActiveAndEnabled
                || renderer.asset == null || renderer.asset.clusters == null
                || renderer.asset.clusters.Length == 0)
                return false;
            if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded)
                return false;
            if (!IsLayerVisible(renderer.gameObject.layer, Tools.visibleLayers, camera.cullingMask))
                return false;
            if (!stage.Contains(renderer.gameObject))
                return false;
            return !SceneVisibilityManager.instance.IsHidden(renderer.gameObject);
        }

        static void AddRenderer(ClusterMeshRenderer renderer)
        {
            Matrices.Add(renderer.transform.localToWorldMatrix);
            CpuCullFlags.Add(renderer.enableCpuObjectCull);
        }

        static ClusterMeshDrawContext GetOrCreateContext(BatchKey key)
        {
            if (Contexts.TryGetValue(key, out ClusterMeshDrawContext context))
                return context;

            context = new ClusterMeshDrawContext(key.asset, key.cullShader, key.litShader);
            Contexts.Add(key, context);
            return context;
        }

        static void DisposeUnusedContexts()
        {
            StaleContexts.Clear();
            foreach (var pair in Contexts)
            {
                if (!UsedContexts.Contains(pair.Key))
                    StaleContexts.Add(pair.Key);
            }

            DisposeStaleContexts();
        }

        static void DisposeStaleContexts()
        {
            for (int i = 0; i < StaleContexts.Count; i++)
            {
                BatchKey key = StaleContexts[i];
                Contexts[key]?.Dispose();
                Contexts.Remove(key);
            }

            StaleContexts.Clear();
        }
    }
}
