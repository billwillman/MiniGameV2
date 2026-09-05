using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ClusterMesh
{
    public static class ClusterMeshDemoSceneMenu
    {
        public const string ScenePath = "Assets/ClusterMesh/Samples/ClusterMeshDemo.unity";
        public const string AssetPath = "Assets/ClusterMesh/Samples/DemoCube.asset";
        public const int InstanceCount = 10;

        [InitializeOnLoadMethod]
        static void EnsureDemoSceneOnLoad()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += EnsureDemoSceneIfMissing;
        }

        static void EnsureDemoSceneIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (AssetDatabase.LoadAssetAtPath<Object>(ScenePath) != null)
                return;
            BuildAndSaveDemoScene(NewSceneMode.Additive, closeAfterSave: true);
        }

        [MenuItem("Tools/ClusterMesh/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            BuildAndSaveDemoScene(NewSceneMode.Single, closeAfterSave: false);
        }

        static void BuildAndSaveDemoScene(NewSceneMode mode, bool closeAfterSave)
        {
            EnsureSamplesFolder();

            Scene previous = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, mode);
            EditorSceneManager.SetActiveScene(scene);

            var root = CreateDemoObjects();
            EditorSceneManager.MoveGameObjectToScene(root, scene);
            FrameDemoCamera(scene);

            var renderers = root.GetComponentsInChildren<ClusterMeshRenderer>();
            var first = renderers[0];
            var stored = ScriptableObject.CreateInstance<ClusterMeshAsset>();
            stored.CopyPackedFrom(first.asset);
            PersistDemoAsset(stored);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].asset = stored;

            DeleteAssetIfExists(ScenePath);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            if (closeAfterSave)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid())
                    EditorSceneManager.SetActiveScene(previous);
            }
            else
                Selection.activeGameObject = root;
        }

        static void FrameDemoCamera(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var camera = roots[i].GetComponentInChildren<Camera>();
                if (camera == null)
                    continue;
                camera.transform.position = new Vector3(0f, 5f, -18f);
                camera.transform.LookAt(Vector3.zero);
                return;
            }
        }

        public static void EnsureSamplesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh"))
                AssetDatabase.CreateFolder("Assets", "ClusterMesh");
            if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh/Samples"))
                AssetDatabase.CreateFolder("Assets/ClusterMesh", "Samples");
        }

        public static void DeleteAssetIfExists(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        public static void PersistDemoAsset(ClusterMeshAsset asset)
        {
            DeleteAssetIfExists(AssetPath);
            AssetDatabase.CreateAsset(asset, AssetPath);
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
            host.SetActive(false);
            cube.transform.SetParent(host.transform, true);

            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            var camera = Object.FindObjectOfType<Camera>();

            for (int i = 0; i < InstanceCount; i++)
            {
                var child = new GameObject("ClusterMeshInstance_" + i);
                child.transform.SetParent(host.transform, false);
                child.transform.localPosition = new Vector3((i - 4.5f) * 1.5f, 0f, 0f);
                var clusterRenderer = child.AddComponent<ClusterMeshRenderer>();
                clusterRenderer.asset = asset;
                clusterRenderer.cullShader = cull;
                clusterRenderer.litShader = lit;
                clusterRenderer.targetCamera = camera;
            }

            host.SetActive(true);
            ClusterMeshSceneBatcher.Flush();
            return host;
        }
    }
}
