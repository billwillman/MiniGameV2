using UnityEditor;

namespace ClusterMesh
{
    [InitializeOnLoad]
    static class ClusterMeshLifetime
    {
        static ClusterMeshLifetime()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ClusterMeshSceneBatcher.DisposeCachedContexts;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.quitting += ClusterMeshSceneBatcher.DisposeCachedContexts;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ClusterMeshSceneBatcher.DisposeCachedContexts();
        }
    }
}
