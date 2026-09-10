using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [CustomEditor(typeof(ClusterSkinnedMeshAsset))]
    public sealed class ClusterSkinnedMeshAssetEditor : UnityEditor.Editor
    {
        int _previewClip;
        float _previewTime;
        Texture2D _palettePreview;
        Matrix4x4[] _paletteScratch;
        Color[] _palettePixels;
        ClusterSkinnedMeshAsset _lastPreviewAsset;
        int _lastPreviewClip = -1;
        float _lastPreviewTime = -1f;

        void OnDisable()
        {
            if (_palettePreview != null)
                DestroyImmediate(_palettePreview);
            _palettePreview = null;
        }

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
            DrawPalettePreview(asset);
            EditorGUILayout.Space();
            if (GUILayout.Button("加入场景"))
                Selection.activeGameObject = ClusterSkinnedMeshPlaceMenu.CreateInScene(asset);
        }

        void DrawPalettePreview(ClusterSkinnedMeshAsset asset)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("VTF Palette Texture", EditorStyles.boldLabel);
            int boneCount = asset.bindPoses != null ? asset.bindPoses.Length : 0;
            ClusterSkinnedClip[] clips = asset.clips;
            if (boneCount <= 0 || clips == null || clips.Length == 0)
            {
                EditorGUILayout.HelpBox("没有可预览的骨骼动画数据。", MessageType.Info);
                return;
            }

            var labels = new string[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                string clipName = clips[i] != null && !string.IsNullOrEmpty(clips[i].name)
                    ? clips[i].name : "<Missing>";
                labels[i] = i + ": " + clipName;
            }
            _previewClip = EditorGUILayout.Popup("Animation Clip", Mathf.Clamp(_previewClip, 0, clips.Length - 1), labels);
            _previewTime = EditorGUILayout.Slider("Normalized Time", _previewTime, 0f, 1f);

            int width = boneCount * 3;
            if (width > SystemInfo.maxTextureSize)
            {
                EditorGUILayout.HelpBox("骨骼 Palette 宽度超过当前设备的最大纹理尺寸，无法创建 VTF 预览。", MessageType.Error);
                return;
            }
            EditorGUILayout.LabelField("Runtime Layout", width + " × instance count · RGBAFloat · Point");
            EditorGUILayout.HelpBox(
                "横向每个骨骼占 3 个像素，分别保存 3×4 蒙皮矩阵的 Row 0/1/2。下面显示当前 Clip 与时间的一条实例纹理行；负值和大于 1 的值在预览窗口中会被颜色显示范围截断，但底层像素仍是原始 float。",
                MessageType.None);

            if (asset.HasGpuPalette(_previewClip))
            {
                Texture2D atlas = asset.gpuPaletteTextures[_previewClip];
                EditorGUILayout.LabelField("Baked GPU Atlas",
                    atlas.width + " × " + atlas.height + " · " + atlas.format + " · " + atlas.filterMode);
                Rect atlasRect = GUILayoutUtility.GetRect(64f, 96f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(atlasRect, atlas, ScaleMode.StretchToFill, false);
                EditorGUILayout.ObjectField("GPU Texture Asset", atlas, typeof(Texture2D), false);
            }
            else
            {
                EditorGUILayout.HelpBox("该 Clip 没有 GPU Palette Atlas；GPU 模式会自动回退 CPU。请重新 Baker。", MessageType.Warning);
            }

            EnsurePalettePreview(asset, boneCount, width);
            if (_palettePreview == null)
                return;
            Rect previewRect = GUILayoutUtility.GetRect(64f, 72f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(previewRect, _palettePreview, ScaleMode.StretchToFill, false);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Current Palette Row", _palettePreview, typeof(Texture2D), false);
        }

        void EnsurePalettePreview(ClusterSkinnedMeshAsset asset, int boneCount, int width)
        {
            bool textureChanged = _palettePreview == null || _palettePreview.width != width;
            if (textureChanged)
            {
                if (_palettePreview != null)
                    DestroyImmediate(_palettePreview);
                _palettePreview = new Texture2D(width, 1, TextureFormat.RGBAFloat, false, true)
                {
                    name = "ClusterSkinnedMesh VTF Preview (Editor Only)",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _paletteScratch = new Matrix4x4[boneCount];
                _palettePixels = new Color[width];
            }
            else if (_paletteScratch == null || _paletteScratch.Length != boneCount)
            {
                _paletteScratch = new Matrix4x4[boneCount];
                _palettePixels = new Color[width];
                textureChanged = true;
            }

            bool poseChanged = textureChanged || _lastPreviewAsset != asset ||
                _lastPreviewClip != _previewClip || !Mathf.Approximately(_lastPreviewTime, _previewTime);
            if (!poseChanged)
                return;
            if (!ClusterSkinnedAnimation.EvaluatePalette(asset, _previewClip, _previewTime, _paletteScratch))
                return;

            for (int bone = 0; bone < boneCount; bone++)
            {
                Matrix4x4 matrix = _paletteScratch[bone];
                int pixel = bone * 3;
                _palettePixels[pixel] = new Color(matrix.m00, matrix.m01, matrix.m02, matrix.m03);
                _palettePixels[pixel + 1] = new Color(matrix.m10, matrix.m11, matrix.m12, matrix.m13);
                _palettePixels[pixel + 2] = new Color(matrix.m20, matrix.m21, matrix.m22, matrix.m23);
            }
            _palettePreview.SetPixels(_palettePixels);
            _palettePreview.Apply(false, false);
            _lastPreviewAsset = asset;
            _lastPreviewClip = _previewClip;
            _lastPreviewTime = _previewTime;
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
