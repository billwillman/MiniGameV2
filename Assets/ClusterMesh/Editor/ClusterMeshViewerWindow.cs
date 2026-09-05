using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClusterMesh
{
    public sealed class ClusterMeshViewerWindow : EditorWindow
    {
        ClusterMeshAsset _asset;
        ClusterMeshDrawContext _context;
        PreviewRenderUtility _preview;
        Vector2 _orbit = new Vector2(30f, 0f);
        float _distance = 2.2f;
        int _isolate = -1;
        bool _showAabb;
        bool _coneCull = true;
        Vector2 _scroll;
        Material _aabbMat;

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
            if (_aabbMat != null)
            {
                DestroyImmediate(_aabbMat);
                _aabbMat = null;
            }
        }

        void OnEnable()
        {
            if (_asset != null && _context == null)
                RebuildContext();
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            _asset = (ClusterMeshAsset)EditorGUILayout.ObjectField("Asset", _asset, typeof(ClusterMeshAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                _distance = 2.2f;
                RebuildContext();
            }

            if (_asset != null && _context == null)
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
            _showAabb = EditorGUILayout.Toggle("Show AABB", _showAabb);
            EditorGUI.BeginChangeCheck();
            _coneCull = EditorGUILayout.Toggle("Cone Cull", _coneCull);
            if (EditorGUI.EndChangeCheck())
                Repaint();
            if (_context != null)
            {
                _context.IsolateIndex = _isolate;
                _context.EnableConeCull = _coneCull;
            }
            if (!_coneCull)
                EditorGUILayout.HelpBox("Cone Cull 已关。Viewer 和场景用同一条 compute；这里误剔，场景里同一套也会剔。对比完再打开。", MessageType.Warning);

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
            _preview.camera.fieldOfView = 50f;
            _preview.lights[0].intensity = 1.2f;
            _preview.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        }

        void DrawPreview(Rect rect)
        {
            if (_context == null || !_context.IsReady || rect.width < 8f || rect.height < 8f)
                return;

            HandleOrbit(rect);
            EnsurePreview();
            Bounds bounds = ClusterMeshFrustum.AssetLocalBounds(_asset);
            Vector3 look = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            _preview.camera.nearClipPlane = Mathf.Max(0.01f, radius * 0.002f);
            _preview.camera.farClipPlane = Mathf.Max(100f, radius * 40f);
            _preview.BeginPreview(rect, GUIStyle.none);
            _preview.camera.transform.position = look + Quaternion.Euler(_orbit.x, _orbit.y, 0f) * new Vector3(0f, 0f, -radius * _distance);
            _preview.camera.transform.LookAt(look);
            _context.Draw(Matrix4x4.identity, _preview.camera);
            _preview.camera.Render();
            if (_showAabb)
                DrawAabbOverlay();
            var tex = _preview.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        void DrawAabbOverlay()
        {
            if (_asset == null || _asset.clusters == null)
                return;
            if (_aabbMat == null)
            {
                var shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                    return;
                _aabbMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                _aabbMat.SetInt("_ZWrite", 0);
                _aabbMat.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            }

            _aabbMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(_preview.camera.projectionMatrix);
            GL.modelview = _preview.camera.worldToCameraMatrix;
            GL.Begin(GL.LINES);
            GL.Color(new Color(0.2f, 1f, 0.35f, 1f));
            for (int i = 0; i < _asset.clusters.Length; i++)
            {
                if (_isolate >= 0 && i != _isolate)
                    continue;
                ClusterHeader h = _asset.clusters[i];
                DrawWireBox(h.aabbCenter, h.aabbExtents);
            }

            GL.End();
            GL.PopMatrix();
        }

        static void DrawWireBox(Vector3 center, Vector3 extents)
        {
            Vector3 a = center + new Vector3(-extents.x, -extents.y, -extents.z);
            Vector3 b = center + new Vector3(extents.x, -extents.y, -extents.z);
            Vector3 c = center + new Vector3(extents.x, extents.y, -extents.z);
            Vector3 d = center + new Vector3(-extents.x, extents.y, -extents.z);
            Vector3 e = center + new Vector3(-extents.x, -extents.y, extents.z);
            Vector3 f = center + new Vector3(extents.x, -extents.y, extents.z);
            Vector3 g = center + new Vector3(extents.x, extents.y, extents.z);
            Vector3 h = center + new Vector3(-extents.x, extents.y, extents.z);
            Line(a, b); Line(b, c); Line(c, d); Line(d, a);
            Line(e, f); Line(f, g); Line(g, h); Line(h, e);
            Line(a, e); Line(b, f); Line(c, g); Line(d, h);
        }

        static void Line(Vector3 a, Vector3 b)
        {
            GL.Vertex(a);
            GL.Vertex(b);
        }

        void HandleOrbit(Rect rect)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                _distance = Mathf.Clamp(_distance * (1f + e.delta.y * 0.03f), 0.4f, 25f);
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
