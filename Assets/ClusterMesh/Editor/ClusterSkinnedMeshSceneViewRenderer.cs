using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClusterMesh
{
    /// <summary>Editor-only SceneView bridge for the isolated skinned ClusterMesh path.</summary>
    // Passive editor renderer. ClusterMeshLifetime owns the single global editor/camera callbacks
    // and dispatches to both static and skinned preview paths.
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
        static readonly List<Matrix4x4> PreviousMatrices = new List<Matrix4x4>(64);
        static readonly List<bool> CpuCull = new List<bool>(64);
        static readonly List<bool> CameraCull = new List<bool>(64);
        static readonly List<float> Times = new List<float>(64);
        static readonly List<float> PreviousTimes = new List<float>(64);
        static readonly List<bool> MotionVectorFlags = new List<bool>(64);
        static readonly List<bool> LightProbeFlags = new List<bool>(64);
        static readonly List<bool> AmbientSkyFlags = new List<bool>(64);
        static readonly List<bool> FogFlags = new List<bool>(64);

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
                Matrices.Clear(); PreviousMatrices.Clear(); CpuCull.Clear(); CameraCull.Clear();
                Times.Clear(); PreviousTimes.Clear(); MotionVectorFlags.Clear();
                LightProbeFlags.Clear(); AmbientSkyFlags.Clear(); FogFlags.Clear();
                Matrix4x4 currentMatrix = r.transform.localToWorldMatrix;
                float currentTime = r.CurrentNormalizedTime(Application.isPlaying ? Time.time : (float)editorTime);
                r.CapturePreviousMotion(currentMatrix, currentTime, r.clipIndex, Time.frameCount,
                    out Matrix4x4 previousMatrix, out float previousTime);
                Matrices.Add(currentMatrix); PreviousMatrices.Add(previousMatrix);
                CpuCull.Add(r.enableCpuObjectCull); CameraCull.Add(r.enableCameraCull);
                Times.Add(currentTime); PreviousTimes.Add(previousTime); MotionVectorFlags.Add(r.enableMotionVectors);
                LightProbeFlags.Add(r.enableLightProbes); AmbientSkyFlags.Add(r.enableAmbientSky); FogFlags.Add(r.enableFog);
                entry.context.EnableClusterColor = r.showClusterColors;
                entry.context.DrawMotion(Matrices, PreviousMatrices, CpuCull, CameraCull, Times, PreviousTimes,
                    MotionVectorFlags, r.clipIndex, r.animationEvaluation,
                    r.enableParallelBonePrefix, r.enableConeCull,
                    r.lodErrorThreshold, cullCamera, drawCamera, r.castShadows, r.receiveShadows, r.gameObject.layer,
                    LightProbeFlags, AmbientSkyFlags, FogFlags);
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

        public static void DisposeCachedContexts()
        {
            foreach (var pair in Contexts) pair.Value.context?.Dispose();
            Contexts.Clear();
            ActiveRendererIds.Clear();
            StaleContextIds.Clear();
            Renderers.Clear();
        }
    }
}
