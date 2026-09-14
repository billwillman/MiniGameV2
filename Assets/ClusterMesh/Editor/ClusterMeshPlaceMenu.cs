using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public static class ClusterMeshPlaceMenu
    {
        public const string DefaultObjectName = "Cluster Mesh";
        const string CullPath = "Assets/ClusterMesh/Shaders/ClusterMeshCull.compute";

        [MenuItem("GameObject/ClusterMesh/Cluster Mesh", false, 10)]
        public static void CreateFromGameObjectMenu()
        {
            CreateFromSelection(true);
        }

        [MenuItem("Assets/ClusterMesh/Add to Scene", false, 2000)]
        public static void CreateFromAssetsMenu()
        {
            CreateFromSelection(false);
        }

        [MenuItem("Assets/ClusterMesh/Add to Scene", true)]
        public static bool ValidateCreateFromAssetsMenu()
        {
            return Selection.GetFiltered<ClusterMeshAsset>(SelectionMode.Assets).Length > 0;
        }

        public static GameObject[] CreateFromSelection(bool allowEmpty)
        {
            var assets = Selection.GetFiltered<ClusterMeshAsset>(SelectionMode.Assets);
            Transform parent = SceneParent();
            GameObject[] created;
            if (assets.Length == 0)
            {
                if (!allowEmpty)
                    return System.Array.Empty<GameObject>();
                created = CreateInScene(new ClusterMeshAsset[] { null }, parent);
            }
            else
                created = CreateInScene(assets, parent);

            Selection.objects = created;
            return created;
        }

        public static GameObject CreateInScene(ClusterMeshAsset asset)
        {
            return CreateInScene(new[] { asset }, null)[0];
        }

        public static GameObject[] CreateInScene(IList<ClusterMeshAsset> assets)
        {
            return CreateInScene(assets, null);
        }

        public static GameObject[] CreateInScene(IList<ClusterMeshAsset> assets, Transform parent)
        {
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Cluster Mesh");

            GameObject[] created;
            if (assets == null || assets.Count == 0)
                created = new[] { SpawnOne(null, PlacementOrigin(), parent) };
            else
            {
                var widths = new float[assets.Count];
                float total = 0f;
                for (int i = 0; i < assets.Count; i++)
                {
                    widths[i] = SpacingFor(assets[i]);
                    total += widths[i];
                }

                Vector3 origin = PlacementOrigin();
                float cursor = -total * 0.5f + widths[0] * 0.5f;
                created = new GameObject[assets.Count];
                for (int i = 0; i < assets.Count; i++)
                {
                    if (i > 0)
                        cursor += (widths[i - 1] + widths[i]) * 0.5f;
                    created[i] = SpawnOne(assets[i], origin + Vector3.right * cursor, parent);
                }
            }

            Undo.CollapseUndoOperations(group);
            return created;
        }

        static GameObject SpawnOne(ClusterMeshAsset asset, Vector3 position, Transform parent)
        {
            string name = asset != null && !string.IsNullOrEmpty(asset.name) ? asset.name : DefaultObjectName;
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add Cluster Mesh");
            if (parent != null)
                Undo.SetTransformParent(go.transform, parent, "Add Cluster Mesh");
            go.transform.position = position;

            var renderer = go.AddComponent<ClusterMeshRenderer>();
            renderer.asset = asset;
            renderer.cullShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(CullPath);
            renderer.litShader = Shader.Find("ClusterMesh/Lit");
            renderer.EnsureInitialized();
            return go;
        }

        static Transform SceneParent()
        {
            Transform t = Selection.activeTransform;
            if (t == null || !t.gameObject.scene.IsValid())
                return null;
            return t;
        }

        static Vector3 PlacementOrigin()
        {
            SceneView view = SceneView.lastActiveSceneView;
            return view != null ? view.pivot : Vector3.zero;
        }

        static float SpacingFor(ClusterMeshAsset asset)
        {
            if (asset == null || asset.clusters == null || asset.clusters.Length == 0)
                return 2f;
            float width = ClusterMeshFrustum.AssetLocalBounds(asset).size.x;
            return Mathf.Max(2f, width + 0.5f);
        }
    }
}
