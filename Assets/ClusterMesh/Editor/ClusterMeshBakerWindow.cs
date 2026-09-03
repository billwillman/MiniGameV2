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
            _mesh = (Mesh)EditorGUILayout.ObjectField("Mesh", _mesh, typeof(Mesh), false);
            _sourceObject = (GameObject)EditorGUILayout.ObjectField("MeshFilter Object", _sourceObject, typeof(GameObject), true);
            _outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", _outputFolder, typeof(DefaultAsset), false);
            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _settings.maxVerticesPerCluster = EditorGUILayout.IntField("Max Vertices", _settings.maxVerticesPerCluster);
            _settings.maxTrianglesPerCluster = EditorGUILayout.IntField("Max Triangles", _settings.maxTrianglesPerCluster);

            if (GUILayout.Button("Bake"))
                Bake();

            if (!string.IsNullOrEmpty(_error))
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (!string.IsNullOrEmpty(_info))
                EditorGUILayout.HelpBox(_info, MessageType.Info);
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
                        throw new InvalidOperationException("v1 only accepts MeshFilter. Skinned meshes are not supported.");
                    var filter = _sourceObject.GetComponent<MeshFilter>();
                    if (filter.GetComponent<SkinnedMeshRenderer>() != null && filter.sharedMesh == null)
                        throw new InvalidOperationException("Skinned meshes are not supported in v1.");
                    mesh = filter.sharedMesh;
                    var renderer = _sourceObject.GetComponent<MeshRenderer>();
                    if (renderer != null)
                        materials = renderer.sharedMaterials;
                }

                if (mesh == null)
                    throw new InvalidOperationException("Assign a Mesh or a MeshFilter object.");

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
                _info = "Wrote " + path + " with " + asset.clusters.Length + " clusters.";
                Selection.activeObject = asset;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }
    }
}
