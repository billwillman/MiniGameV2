using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>
    /// Submits ClusterMesh draws for Scene view cameras in both Edit and Play modes.
    /// Kept in the Editor assembly so player rendering and builds are unaffected.
    /// </summary>
    public static class ClusterMeshSceneViewRenderer
    {
        readonly struct BatchKey : IEquatable<BatchKey>
        {
            public readonly ClusterMeshAsset asset;
            public readonly Camera drawCamera;
            public readonly Camera cullingCamera;
            public readonly ComputeShader cullShader;
            public readonly Shader litShader;
            public readonly int assetId;
            public readonly int cameraId;
            public readonly int cullingCameraId;
            public readonly int cullShaderId;
            public readonly int litShaderId;
            public readonly int layer;
            public readonly bool enableConeCull;
            public readonly bool showClusterColors;
            public readonly float lodErrorThreshold;
            public readonly bool castShadows;
            public readonly bool receiveShadows;

            public BatchKey(
                ClusterMeshRenderer renderer,
                Camera sceneCamera,
                Camera gameCamera,
                ComputeShader cull,
                Shader lit)
            {
                asset = renderer.asset;
                drawCamera = sceneCamera;
                cullingCamera = gameCamera;
                cullShader = cull;
                litShader = lit;
                assetId = asset.GetInstanceID();
                cameraId = drawCamera.GetInstanceID();
                cullingCameraId = cullingCamera.GetInstanceID();
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
                    && cullingCameraId == other.cullingCameraId
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
                    hash = (hash * 397) ^ cullingCameraId;
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
        static readonly List<bool> CameraCullFlags = new List<bool>(64);

        static readonly List<ClusterMeshRenderer> Renderers = new List<ClusterMeshRenderer>(64);

        public static int CachedContextCount => Contexts.Count;

        public static bool IsLayerVisible(int layer, int editorVisibleLayers, int cameraCullingMask)
        {
            int layerMask = 1 << layer;
            return (editorVisibleLayers & layerMask) != 0
                && (cameraCullingMask & layerMask) != 0;
        }

        public static void RefreshAndRepaint()
        {
            ClusterMeshSceneBatcher.CollectRegisteredRenderersForEditor(Renderers);
            UsedContexts.Clear();
            StageHandle stage = StageUtility.GetCurrentStageHandle();
            var sceneViews = SceneView.sceneViews;
            for (int i = 0; i < sceneViews.Count; i++)
            {
                var sceneView = sceneViews[i] as SceneView;
                Camera camera = sceneView != null ? sceneView.camera : null;
                if (camera == null || camera.cameraType != CameraType.SceneView)
                    continue;

                for (int j = 0; j < Renderers.Count; j++)
                {
                    ClusterMeshRenderer renderer = Renderers[j];
                    if (renderer != null && TryGetBatchKey(renderer, camera, stage, out BatchKey key))
                        UsedContexts.Add(key);
                }
            }

            DisposeUnusedContexts();
            if (Renderers.Count > 0)
                SceneView.RepaintAll();
        }

        public static void DrawSceneCamera(Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.SceneView)
                return;

            ClusterMeshSceneBatcher.CollectRegisteredRenderersForEditor(Renderers);
            ProcessCamera(camera, StageUtility.GetCurrentStageHandle());
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
            CameraCullFlags.Clear();
            Renderers.Clear();
        }

        static void ProcessCamera(Camera camera, StageHandle stage)
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
                CameraCullFlags.Clear();
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
                if (context == null || !context.CanDraw)
                    continue;

                context.EnableConeCull = key.enableConeCull;
                context.EnableClusterColor = key.showClusterColors;
                // Scene view is a culling preview: keep leaf clusters so the compute
                // shader applies cone culling whenever the renderer switch is enabled.
                // Runtime/Game rendering keeps using the configured LOD threshold.
                context.LodErrorThreshold = 0f;
                context.EditorDrawLayer = key.layer;
                context.DrawEditorPreview(
                    Matrices, CpuCullFlags, CameraCullFlags, key.cullingCamera, key.drawCamera,
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
            Camera cullingCamera = renderer.targetCamera != null ? renderer.targetCamera : Camera.main;
            if (cullingCamera == null || !IsSceneVisible(renderer, camera, cullingCamera, stage))
                return false;

            ComputeShader cull = renderer.cullShader;
            if (cull == null)
                cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            Shader lit = renderer.litShader != null ? renderer.litShader : Shader.Find("ClusterMesh/Lit");
            if (cull == null || lit == null)
                return false;

            key = new BatchKey(renderer, camera, cullingCamera, cull, lit);
            return true;
        }

        static bool IsSceneVisible(
            ClusterMeshRenderer renderer,
            Camera drawCamera,
            Camera cullingCamera,
            StageHandle stage)
        {
            if (renderer == null || !renderer.isActiveAndEnabled
                || renderer.asset == null || renderer.asset.clusters == null
                || renderer.asset.clusters.Length == 0)
                return false;
            if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded)
                return false;
            int combinedCameraMask = drawCamera.cullingMask & cullingCamera.cullingMask;
            if (!IsLayerVisible(renderer.gameObject.layer, Tools.visibleLayers, combinedCameraMask))
                return false;
            if (!stage.Contains(renderer.gameObject))
                return false;
            return !SceneVisibilityManager.instance.IsHidden(renderer.gameObject);
        }

        static void AddRenderer(ClusterMeshRenderer renderer)
        {
            Matrices.Add(renderer.transform.localToWorldMatrix);
            CpuCullFlags.Add(renderer.enableCpuObjectCull);
            CameraCullFlags.Add(renderer.enableCameraCull);
        }

        static ClusterMeshDrawContext GetOrCreateContext(BatchKey key)
        {
            if (Contexts.TryGetValue(key, out ClusterMeshDrawContext context))
            {
                if (context != null && context.CanDraw)
                    return context;
                context?.Dispose();
                Contexts.Remove(key);
            }

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
