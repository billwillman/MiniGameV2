using UnityEditor;

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
            EditorApplication.update -= OnEditModeUpdate;
            EditModeTickActive = false;
            if (playingOverride)
                return;

            EditorApplication.update += OnEditModeUpdate;
            EditModeTickActive = true;
        }

        public static void SyncSceneViewTick(bool enabled)
        {
            EditorApplication.update -= OnSceneViewUpdate;
            SceneView.beforeSceneGui -= OnBeforeSceneGui;
            SceneViewTickActive = false;
            if (!enabled)
                return;

            EditorApplication.update += OnSceneViewUpdate;
            SceneView.beforeSceneGui += OnBeforeSceneGui;
            SceneViewTickActive = true;
        }

        static void OnEditModeUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SyncEditModeTick(true);
                return;
            }

            ClusterMeshSceneBatcher.Flush();
            if (ShouldQueuePlayerLoop(ClusterMeshSceneBatcher.RegisteredCount))
                EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnSceneViewUpdate()
        {
            if (EditorApplication.isCompiling)
                return;

            ClusterMeshSceneViewRenderer.RefreshAndRepaint();
        }

        static void OnBeforeSceneGui(SceneView sceneView)
        {
            if (EditorApplication.isCompiling)
                return;

            ClusterMeshSceneViewRenderer.DrawSceneView(sceneView);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SyncEditModeTick(true);
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    SyncEditModeTick(true);
                    SyncSceneViewTick(true);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    ClusterMeshSceneViewRenderer.DisposeCachedContexts();
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
        }

        static void OnQuitting()
        {
            SyncEditModeTick(true);
            SyncSceneViewTick(false);
            ClusterMeshSceneBatcher.DisposeCachedContexts();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
        }
    }
}
