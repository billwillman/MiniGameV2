using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshDemoSceneMenu
    {
        public const string ScenePath = "Assets/ClusterMesh/Samples/ClusterMeshDemo.unity";
        public const string AssetPath = "Assets/ClusterMesh/Samples/DemoCube.asset";

        [MenuItem("Tools/ClusterMesh/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh"))
                AssetDatabase.CreateFolder("Assets", "ClusterMesh");
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh/Samples"))
                AssetDatabase.CreateFolder("Assets/ClusterMesh", "Samples");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = CreateDemoObjects();
            var renderer = root.GetComponentInChildren<ClusterMeshRenderer>();

            var stored = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            stored.CopyFrom(
                new ClusterMeshBakeResult
                {
                    clusters = renderer.asset.clusters,
                    vertices = renderer.asset.vertices,
                    indices = renderer.asset.indices,
                    materials = renderer.asset.materials
                },
                renderer.asset.sourceMesh,
                new ClusterMeshBakeSettings());
            AssetDatabase.CreateAsset(stored, AssetPath);
            renderer.asset = stored;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
        }

        public static GameObject CreateDemoObjects()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SourceCube";
            cube.transform.position = Vector3.zero;
            var filter = cube.GetComponent<MeshFilter>();
            var meshRenderer = cube.GetComponent<MeshRenderer>();
            meshRenderer.enabled = false;

            var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            ClusterMeshBakerWindow.WriteAsset(
                asset,
                filter.sharedMesh,
                meshRenderer.sharedMaterials,
                new ClusterMeshBakeSettings());

            var host = new GameObject("ClusterMeshDemo");
            cube.transform.SetParent(host.transform, true);
            var clusterRenderer = host.AddComponent<ClusterMeshRenderer>();
            clusterRenderer.asset = asset;
            clusterRenderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            clusterRenderer.litShader = Shader.Find("ClusterMesh/Lit");
            clusterRenderer.targetCamera = Object.FindObjectOfType<Camera>();
            return host;
        }
    }
}
