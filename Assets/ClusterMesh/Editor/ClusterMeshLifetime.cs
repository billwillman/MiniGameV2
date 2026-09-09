using UnityEditor;

namespace ClusterMesh
{
    [InitializeOnLoad]
    public static class ClusterMeshLifetime
    {
        public static bool EditModeTickActive { get; private set; }

        static ClusterMeshLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += OnQuitting;
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

        static void OnEditModeUpdate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SyncEditModeTick(true);
                return;
            }

            ClusterMeshSceneBatcher.Flush();
            ClusterMeshSceneViewRenderer.DrawAllSceneViews();
            if (ShouldQueuePlayerLoop(ClusterMeshSceneBatcher.RegisteredCount))
                EditorApplication.QueuePlayerLoopUpdate();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    SyncEditModeTick(true);
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    ClusterMeshSceneViewRenderer.DisposeCachedContexts();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    SyncEditModeTick(true);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ClusterMeshSceneBatcher.DisposeCachedContexts();
                    ClusterMeshSceneViewRenderer.DisposeCachedContexts();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    SyncEditModeTick(false);
                    break;
            }
        }

        static void OnBeforeAssemblyReload()
        {
            SyncEditModeTick(true);
            ClusterMeshSceneBatcher.DisposeCachedContexts();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
        }

        static void OnQuitting()
        {
            SyncEditModeTick(true);
            ClusterMeshSceneBatcher.DisposeCachedContexts();
            ClusterMeshSceneViewRenderer.DisposeCachedContexts();
        }
    }
}
