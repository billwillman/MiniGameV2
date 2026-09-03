using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshViewerWindow : EditorWindow
    {
        ClusterMeshAsset _asset;
        ClusterMeshDrawContext _context;
        PreviewRenderUtility _preview;
        Vector2 _orbit = new Vector2(30f, 0f);
        float _distance = 3f;
        int _isolate = -1;
        Vector2 _scroll;

        [MenuItem("Tools/ClusterMesh/Viewer")]
        public static void Open()
        {
            GetWindow<ClusterMeshViewerWindow>("ClusterMesh Viewer");
        }

        public static ClusterMeshDrawContext CreatePreviewContext(ClusterMeshAsset asset)
        {
            var cull = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            var lit = Shader.Find("ClusterMesh/Lit");
            return new ClusterMeshDrawContext(asset, cull, lit);
        }

        void OnDisable()
        {
            _context?.Dispose();
            _context = null;
            _preview?.Cleanup();
            _preview = null;
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _asset = (ClusterMeshAsset)EditorGUILayout.ObjectField("Asset", _asset, typeof(ClusterMeshAsset), false);
            if (EditorGUI.EndChangeCheck())
                RebuildContext();

            if (_asset == null)
            {
                EditorGUILayout.HelpBox("Assign a ClusterMeshAsset.", MessageType.Info);
                return;
            }

            if (_context != null && !_context.IsReady)
            {
                EditorGUILayout.HelpBox(_context.Error, MessageType.Error);
                return;
            }

            _isolate = EditorGUILayout.IntField("Isolate Cluster (-1 = all)", _isolate);
            if (_context != null)
                _context.IsolateIndex = _isolate;

            Rect previewRect = GUILayoutUtility.GetRect(10f, 320f, GUILayout.ExpandWidth(true));
            DrawPreview(previewRect);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("Clusters", _asset.clusters != null ? _asset.clusters.Length.ToString() : "0");
            EditorGUILayout.LabelField("Vertices", _asset.vertices != null ? _asset.vertices.Length.ToString() : "0");
            EditorGUILayout.LabelField("Indices", _asset.indices != null ? _asset.indices.Length.ToString() : "0");
            EditorGUILayout.LabelField("Materials", _asset.materials != null ? _asset.materials.Length.ToString() : "0");
            EditorGUILayout.EndScrollView();
        }

        void RebuildContext()
        {
            _context?.Dispose();
            _context = _asset != null ? CreatePreviewContext(_asset) : null;
        }

        void EnsurePreview()
        {
            if (_preview != null)
                return;
            _preview = new PreviewRenderUtility();
            _preview.camera.nearClipPlane = 0.01f;
            _preview.camera.farClipPlane = 100f;
            _preview.lights[0].intensity = 1.2f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        }

        void DrawPreview(Rect rect)
        {
            if (_context == null || !_context.IsReady || rect.width < 8f || rect.height < 8f)
                return;

            HandleOrbit(rect);
            EnsurePreview();
            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.transform.position = Quaternion.Euler(_orbit.x, _orbit.y, 0f) * new Vector3(0f, 0f, -_distance);
            _preview.camera.transform.LookAt(Vector3.zero);
            _context.Draw(Matrix4x4.identity, _preview.camera);
            _preview.camera.Render();
            var tex = _preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        void HandleOrbit(Rect rect)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                _distance = Mathf.Clamp(_distance * (1f + e.delta.y * 0.03f), 0.3f, 40f);
                e.Use();
                Repaint();
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition) && e.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        _orbit.y += e.delta.x;
                        _orbit.x = Mathf.Clamp(_orbit.x + e.delta.y, -89f, 89f);
                        e.Use();
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }
    }
}
