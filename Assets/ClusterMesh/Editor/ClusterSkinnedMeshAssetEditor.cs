using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [CustomEditor(typeof(ClusterSkinnedMeshAsset))]
    public sealed class ClusterSkinnedMeshAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var asset = (ClusterSkinnedMeshAsset)target;
            if (asset.animationSamplingVersion != ClusterSkinnedMeshAsset.CurrentAnimationSamplingVersion)
            {
                EditorGUILayout.HelpBox(
                    "该资产使用旧的动画采样数据，请使用 Tools/ClusterMesh/Baker 重新烘焙。",
                    MessageType.Error);
                if (GUILayout.Button("打开 ClusterMesh Baker"))
                    ClusterMeshBakerWindow.Open();
            }
            ClusterMeshAsset geometry = asset.geometry;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clusters", geometry != null && geometry.clusters != null
                ? geometry.clusters.Length.ToString() : "0");
            EditorGUILayout.LabelField("Vertices", geometry != null ? geometry.vertexCount.ToString() : "0");
            EditorGUILayout.LabelField("Bones", asset.bindPoses != null ? asset.bindPoses.Length.ToString() : "0");
            EditorGUILayout.LabelField("Clips", asset.clips != null ? asset.clips.Length.ToString() : "0");
            EditorGUILayout.Space();
            if (GUILayout.Button("加入场景"))
                Selection.activeGameObject = ClusterSkinnedMeshPlaceMenu.CreateInScene(asset);
        }
    }

    public static class ClusterSkinnedMeshPlaceMenu
    {
        const string CullPath = "Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute";

        [MenuItem("GameObject/ClusterMesh/Skinned Cluster Mesh", false, 11)]
        public static void CreateEmpty()
        {
            Selection.activeGameObject = Spawn(null, SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : Vector3.zero);
        }

        [MenuItem("Assets/ClusterMesh/Add Skinned to Scene", false, 2001)]
        public static void CreateSelected()
        {
            ClusterSkinnedMeshAsset asset = Selection.activeObject as ClusterSkinnedMeshAsset;
            if (asset != null)
                Selection.activeGameObject = CreateInScene(asset);
        }

        [MenuItem("Assets/ClusterMesh/Add Skinned to Scene", true)]
        public static bool ValidateCreateSelected()
        {
            return Selection.activeObject is ClusterSkinnedMeshAsset;
        }

        public static GameObject CreateInScene(ClusterSkinnedMeshAsset asset)
        {
            Vector3 position = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            return Spawn(asset, position);
        }

        static GameObject Spawn(ClusterSkinnedMeshAsset asset, Vector3 position)
        {
            var go = new GameObject(asset != null && !string.IsNullOrEmpty(asset.name)
                ? asset.name : "Skinned Cluster Mesh");
            Undo.RegisterCreatedObjectUndo(go, "Add Skinned Cluster Mesh");
            if (Selection.activeTransform != null && Selection.activeTransform.gameObject.scene.IsValid())
                Undo.SetTransformParent(go.transform, Selection.activeTransform, "Add Skinned Cluster Mesh");
            go.transform.position = position;
            var renderer = go.AddComponent<ClusterSkinnedMeshRenderer>();
            renderer.asset = asset;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(CullPath);
            renderer.litShader = Shader.Find("ClusterMesh/SkinnedLit");
            renderer.EnsureInitialized();
            return go;
        }
    }
}
