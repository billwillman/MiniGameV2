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
                _preview.camera.nearClipPlane = 0.01f;
                _preview.camera.farClipPlane = 100f;
            }

            _preview.BeginPreview(r, background);
            _preview.camera.transform.position = new Vector3(0f, 0.75f, -2.4f);
            _preview.camera.transform.LookAt(Vector3.zero);
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
            EditorGUILayout.LabelField("Vertices", asset.vertices != null ? asset.vertices.Length.ToString() : "0");
        }
    }
}
