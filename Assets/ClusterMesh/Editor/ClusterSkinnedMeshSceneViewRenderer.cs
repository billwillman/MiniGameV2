using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>Editor-only SceneView bridge for the isolated skinned ClusterMesh path.</summary>
    [InitializeOnLoad]
    public static class ClusterSkinnedMeshSceneViewRenderer
    {
        static readonly List<ClusterSkinnedMeshRenderer> Renderers = new List<ClusterSkinnedMeshRenderer>();
        sealed class PreviewEntry
        {
            public ClusterSkinnedMeshAsset asset;
            public ComputeShader cullShader;
            public Shader litShader;
            public ClusterSkinnedMeshDrawContext context;
        }

        static readonly Dictionary<int, PreviewEntry> Contexts = new Dictionary<int, PreviewEntry>();
        static readonly HashSet<int> ActiveRendererIds = new HashSet<int>();
        static readonly List<int> StaleContextIds = new List<int>();
        static readonly List<Matrix4x4> Matrices = new List<Matrix4x4>(64);
        static readonly List<bool> CpuCull = new List<bool>(64);
        static readonly List<bool> CameraCull = new List<bool>(64);
        static readonly List<float> Times = new List<float>(64);

        static ClusterSkinnedMeshSceneViewRenderer()
        {
            EditorApplication.update += RefreshAndRepaint;
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Camera.onPreCull += OnBuiltInPreCull;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        static void OnBeginCameraRendering(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera) => DrawSceneCamera(camera);
        static void OnBuiltInPreCull(Camera camera)
        {
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) DrawSceneCamera(camera);
        }

        public static void RefreshAndRepaint()
        {
            if (EditorApplication.isCompiling) return;
            ClusterSkinnedMeshSceneBatcher.CollectRegisteredForEditor(Renderers);
            ActiveRendererIds.Clear();
            for (int i = 0; i < Renderers.Count; i++)
                if (Renderers[i] != null) ActiveRendererIds.Add(Renderers[i].GetInstanceID());
            StaleContextIds.Clear();
            foreach (KeyValuePair<int, PreviewEntry> pair in Contexts)
                if (!ActiveRendererIds.Contains(pair.Key)) StaleContextIds.Add(pair.Key);
            for (int i = 0; i < StaleContextIds.Count; i++)
            {
                int id = StaleContextIds[i];
                Contexts[id].context?.Dispose();
                Contexts.Remove(id);
            }
            if (Renderers.Count > 0) SceneView.RepaintAll();
        }

        public static void DrawSceneCamera(Camera drawCamera)
        {
            if (drawCamera == null || drawCamera.cameraType != CameraType.SceneView || EditorApplication.isCompiling) return;
            ClusterSkinnedMeshSceneBatcher.CollectRegisteredForEditor(Renderers);
            StageHandle stage = StageUtility.GetCurrentStageHandle();
            double editorTime = EditorApplication.timeSinceStartup;
            for (int i = 0; i < Renderers.Count; i++)
            {
                ClusterSkinnedMeshRenderer r = Renderers[i];
                if (!Visible(r, drawCamera, stage)) continue;
                // Deliberately one renderer at a time: clip/cull switches stay exact in the preview.
                int key = r.GetInstanceID();
                if (!Contexts.TryGetValue(key, out PreviewEntry entry) || entry.asset != r.asset ||
                    entry.cullShader != r.cullShader || entry.litShader != r.litShader ||
                    entry.context == null || !entry.context.CanDraw)
                {
                    entry?.context?.Dispose();
                    ClusterSkinnedMeshDrawContext ctx = ClusterSkinnedMeshSceneBatcher.CreatePreviewContext(r);
                    if (ctx == null || !ctx.IsReady) { ctx?.Dispose(); continue; }
                    entry = new PreviewEntry
                    {
                        asset = r.asset,
                        cullShader = r.cullShader,
                        litShader = r.litShader,
                        context = ctx
                    };
                    Contexts[key] = entry;
                }
                Camera cullCamera = r.targetCamera != null ? r.targetCamera : Camera.main;
                if (cullCamera == null) continue;
                Matrices.Clear(); CpuCull.Clear(); CameraCull.Clear(); Times.Clear();
                Matrices.Add(r.transform.localToWorldMatrix); CpuCull.Add(r.enableCpuObjectCull); CameraCull.Add(r.enableCameraCull);
                Times.Add(r.CurrentNormalizedTime(Application.isPlaying ? Time.time : (float)editorTime));
                entry.context.EnableClusterColor = r.showClusterColors;
                entry.context.Draw(Matrices, CpuCull, CameraCull, Times, r.clipIndex, r.enableConeCull,
                    r.lodErrorThreshold, cullCamera, drawCamera, r.castShadows, r.receiveShadows, r.gameObject.layer);
            }
        }

        static bool Visible(ClusterSkinnedMeshRenderer r, Camera drawCamera, StageHandle stage)
        {
            if (r == null || !r.isActiveAndEnabled || r.asset == null || r.asset.geometry == null || !r.gameObject.scene.isLoaded) return false;
            int mask = 1 << r.gameObject.layer;
            Camera cullCamera = r.targetCamera != null ? r.targetCamera : Camera.main;
            return cullCamera != null && (Tools.visibleLayers & mask) != 0 &&
                   (drawCamera.cullingMask & cullCamera.cullingMask & mask) != 0 &&
                   stage.Contains(r.gameObject) && !SceneVisibilityManager.instance.IsHidden(r.gameObject);
        }

        static void Dispose()
        {
            foreach (var pair in Contexts) pair.Value.context?.Dispose();
            Contexts.Clear();
        }
    }
}
