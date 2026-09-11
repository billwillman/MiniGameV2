using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    [InitializeOnLoad]
    public static class ClusterMeshLifetime
    {
        public static bool EditModeTickActive { get; private set; }
        public static bool SceneViewTickActive { get; private set; }

        static ClusterMeshLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnQuitting;
            SyncSceneViewTick(true);
            SyncEditModeTick();
        }

        public static bool ShouldQueuePlayerLoop(int registeredCount)
        {
            return registeredCount > 0;
        }

        public static void SyncEditModeTick()
        {
            SyncEditModeTick(EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode);
        }

        public static void SyncEditModeTick(bool playingOverride)
        {
            EditModeTickActive = !playingOverride;
            SyncEditorUpdateSubscription();
        }

        public static void SyncSceneViewTick(bool enabled)
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Camera.onPreCull -= OnBuiltInCameraPreCull;
            SceneViewTickActive = enabled;
            if (enabled)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
                Camera.onPreCull += OnBuiltInCameraPreCull;
            }
            SyncEditorUpdateSubscription();
        }

        static void SyncEditorUpdateSubscription()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (EditModeTickActive || SceneViewTickActive)
                EditorApplication.update += OnEditorUpdate;
        }

        static void OnEditorUpdate()
        {
            if (EditModeTickActive)
                OnEditModeUpdate();
            if (SceneViewTickActive)
                OnSceneViewUpdate();
        }

        static void OnEditModeUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SyncEditModeTick(true);
                return;
            }

            ClusterMeshSceneBatcher.Flush();
            ClusterSkinnedMeshSceneBatcher.Flush();
            if (ShouldQueuePlayerLoop(ClusterMeshSceneBatcher.RegisteredCount) ||
                ShouldQueuePlayerLoop(ClusterSkinnedMeshSceneBatcher.RegisteredCount))
                EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnSceneViewUpdate()
        {
            if (EditorApplication.isCompiling)
                return;

            ClusterMeshSceneViewRenderer.RefreshAndRepaint();
            ClusterSkinnedMeshSceneViewRenderer.RefreshAndRepaint();
        }

        static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (EditorApplication.isCompiling)
                return;

            ClusterMeshSceneViewRenderer.DrawSceneCamera(camera);
            ClusterSkinnedMeshSceneViewRenderer.DrawSceneCamera(camera);
            ClusterMeshUrpBridge.SubmitUrpShadowsBeforeCull(camera);
        }

        static void OnBuiltInCameraPreCull(Camera camera)
        {
            if (EditorApplication.isCompiling || GraphicsSettings.currentRenderPipeline != null)
                return;

            ClusterMeshSceneViewRenderer.DrawSceneCamera(camera);
            ClusterSkinnedMeshSceneViewRenderer.DrawSceneCamera(camera);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SyncEditModeTick(true);
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    ClusterMeshSceneViewRenderer.DisposeCachedContexts();
                    ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
                    ClusterSkinnedMeshSceneViewRenderer.DisposeCachedContexts();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    SyncEditModeTick(true);
                    SyncSceneViewTick(true);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    ClusterMeshSceneViewRenderer.DisposeCachedContexts();
                    ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
                    ClusterSkinnedMeshSceneViewRenderer.DisposeCachedContexts();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SyncSceneViewTick(true);
                    SyncEditModeTick(false);
                    break;
            }
        }

        static void OnBeforeAssemblyReload()
        {
            SyncEditModeTick(true);
            SyncSceneViewTick(false);
            ClusterMeshSceneBatcher.DisposeCachedContexts();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
            ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
            ClusterSkinnedMeshSceneViewRenderer.DisposeCachedContexts();
        }

        static void OnQuitting()
        {
            SyncEditModeTick(true);
            SyncSceneViewTick(false);
            ClusterMeshSceneBatcher.DisposeCachedContexts();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
            ClusterSkinnedMeshSceneBatcher.DisposeCachedContexts();
            ClusterSkinnedMeshSceneViewRenderer.DisposeCachedContexts();
        }
    }
}
