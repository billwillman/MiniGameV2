using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [CustomEditor(typeof(ClusterMeshAsset))]
    public sealed class ClusterMeshAssetEditor : UnityEditor.Editor
    {
        ClusterMeshDrawContext _context;
        PreviewRenderUtility _preview;

        void OnDisable()
        {
            _context?.Dispose();
            _context = null;
            _preview?.Cleanup();
            _preview = null;
        }

        public override bool HasPreviewGUI()
        {
            return true;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var asset = (ClusterMeshAsset)target;
            if (_context == null)
                _context = ClusterMeshViewerWindow.CreatePreviewContext(asset);
            if (_context == null || !_context.IsReady)
            {
                EditorGUI.LabelField(r, _context != null ? _context.Error : "Preview unavailable");
                return;
            }

            if (_preview == null)
            {
                _preview = new PreviewRenderUtility();
                _preview.camera.fieldOfView = 50f;
            }

            Bounds bounds = ClusterMeshFrustum.AssetLocalBounds(asset);
            Vector3 look = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            _preview.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.002f);
            _preview.camera.farClipPlane = Mathf.Max(100f, radius * 40f);
            _preview.BeginPreview(r, background);
            _preview.camera.transform.position = look + new Vector3(0f, radius * 0.35f, -radius * 2.4f);
            _preview.camera.transform.LookAt(look);
            _context.Draw(Matrix4x4.identity, _preview.camera);
            _preview.camera.Render();
            GUI.DrawTexture(r, _preview.EndPreview(), ScaleMode.StretchToFill, false);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var asset = (ClusterMeshAsset)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clusters", asset.clusters != null ? asset.clusters.Length.ToString() : "0");
            EditorGUILayout.LabelField("Vertices", asset.vertexCount.ToString());
            EditorGUILayout.LabelField("Indices", asset.indexCount.ToString());
            EditorGUILayout.Space();
            if (GUILayout.Button("加入场景"))
            {
                var created = ClusterMeshPlaceMenu.CreateInScene(asset);
                Selection.activeGameObject = created;
            }
        }
    }
}
