using System;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshBakerWindow : EditorWindow
    {
        Mesh _mesh;
        GameObject _sourceObject;
        DefaultAsset _outputFolder;
        string _assetName = "ClusterMeshAsset";
        ClusterMeshBakeSettings _settings = new ClusterMeshBakeSettings();
        string _error;
        string _info;
        Vector2 _scroll;

        [MenuItem("Tools/ClusterMesh/Baker")]
        public static void Open()
        {
            GetWindow<ClusterMeshBakerWindow>("ClusterMesh Baker");
        }

        public static void WriteAsset(ClusterMeshAsset asset, Mesh mesh, Material[] materials, ClusterMeshBakeSettings settings)
        {
            var result = ClusterMeshBaker.Bake(mesh, materials, settings);
            asset.CopyFrom(result, mesh, settings);
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("怎么用", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "把一张静态 Mesh 拆成 cluster，存成 ClusterMeshAsset，给 ClusterMeshRenderer 或 Viewer 用。\n\n" +
                "1. 拖一张 Mesh，或拖场景里带 MeshFilter 的物体（会用它的 Mesh 和材质）。\n" +
                "2. 选输出目录。空着则写到 Assets/ClusterMesh/Samples。\n" +
                "3. 填资产名，点 Bake。成功后 Project 会选中生成的 .asset。\n" +
                "4. 新建空物体，加 ClusterMeshRenderer，把 .asset 拖到 Asset。\n" +
                "5. 看效果：打开 Tools/ClusterMesh/Viewer 拖同一个资产；或 Tools/ClusterMesh/Create Demo Scene。\n\n" +
                "限制：只要 MeshFilter，不要 SkinnedMesh。默认每 cluster 最多 64 顶点 / 124 三角。\n" +
                "Stats 里 SetPass/Batches 会含阴影、Depth、多相机。合批看 Frame Debugger 的 DrawMeshInstancedIndirect。阴影可在 ClusterMeshRenderer 上关 Cast Shadows。",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输入", EditorStyles.boldLabel);
            _mesh = (Mesh)EditorGUILayout.ObjectField(
                new GUIContent("Mesh", "直接指定要拆的网格。和下面的物体二选一，物体优先。"),
                _mesh, typeof(Mesh), false);
            _sourceObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("MeshFilter 物体", "从场景/预制体拖一个带 MeshFilter 的物体，自动取 Mesh 和材质。"),
                _sourceObject, typeof(GameObject), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("输出目录", "必须是 Project 里的文件夹。空 = Assets/ClusterMesh/Samples。"),
                _outputFolder, typeof(DefaultAsset), false);
            _assetName = EditorGUILayout.TextField(
                new GUIContent("资产名", "生成 Xxx.asset。重名会自动加数字。"),
                _assetName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Cluster 预算", EditorStyles.boldLabel);
            _settings.maxVerticesPerCluster = EditorGUILayout.IntField(
                new GUIContent("Max Vertices", "每个 cluster 最多多少顶点。默认 64。"),
                _settings.maxVerticesPerCluster);
            _settings.maxTrianglesPerCluster = EditorGUILayout.IntField(
                new GUIContent("Max Triangles", "每个 cluster 最多多少三角。默认 124。"),
                _settings.maxTrianglesPerCluster);

            EditorGUILayout.Space();
            if (GUILayout.Button("Bake", GUILayout.Height(28)))
                Bake();

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (!string.IsNullOrEmpty(_info))
                EditorGUILayout.HelpBox(_info + "\n下一步：选中这个资产 → 加到 ClusterMeshRenderer.asset，或打开 Tools/ClusterMesh/Viewer。", MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        void Bake()
        {
            _error = null;
            _info = null;
            try
            {
                Mesh mesh = _mesh;
                Material[] materials = null;
                if (_sourceObject != null)
                {
                    if (_sourceObject.GetComponent<MeshFilter>() == null)
                        throw new InvalidOperationException("只支持 MeshFilter，不支持 SkinnedMesh。");
                    var filter = _sourceObject.GetComponent<MeshFilter>();
                    if (filter.GetComponent<SkinnedMeshRenderer>() != null && filter.sharedMesh == null)
                        throw new InvalidOperationException("SkinnedMesh 在 v1 不支持。");
                    mesh = filter.sharedMesh;
                    var renderer = _sourceObject.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        materials = renderer.sharedMaterials;
                }

                if (mesh == null)
                    throw new InvalidOperationException("请指定 Mesh，或拖一个带 MeshFilter 的物体。");

                string folder = _outputFolder != null
                    ? AssetDatabase.GetAssetPath(_outputFolder)
                    : "Assets/ClusterMesh/Samples";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh"))
                        AssetDatabase.CreateFolder("Assets", "ClusterMesh");
                    if (!AssetDatabase.IsValidFolder("Assets/ClusterMesh/Samples"))
                        AssetDatabase.CreateFolder("Assets/ClusterMesh", "Samples");
                    folder = "Assets/ClusterMesh/Samples";
                }

                var asset = ScriptableObject.CreateInstance<ClusterMeshAsset>();
                WriteAsset(asset, mesh, materials, _settings);
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + _assetName + ".asset");
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                _info = "已写入 " + path + "，共 " + asset.clusters.Length + " 个 cluster。";
                Selection.activeObject = asset;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }
    }
}
