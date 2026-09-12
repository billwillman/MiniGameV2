using System.IO;
using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshSettingsWindow : EditorWindow
    {
        ClusterMeshSettings _settings;

        [MenuItem("Tools/ClusterMesh/通用设置", priority = 10)]
        public static void Open()
        {
            GetWindow<ClusterMeshSettingsWindow>("ClusterMesh 通用设置");
        }

        public static ClusterMeshSettings GetOrCreate()
        {
            ClusterMeshSettings settings = AssetDatabase.LoadAssetAtPath<ClusterMeshSettings>(
                ClusterMeshSettings.AssetPath);
            if (settings != null)
                return settings;

            string folder = Path.GetDirectoryName(ClusterMeshSettings.AssetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }

            settings = CreateInstance<ClusterMeshSettings>();
            AssetDatabase.CreateAsset(settings, ClusterMeshSettings.AssetPath);
            AssetDatabase.SaveAssets();
            ClusterMeshSettings.ClearCacheForTests();
            return settings;
        }

        void OnEnable()
        {
            _settings = GetOrCreate();
        }

        void OnGUI()
        {
            if (_settings == null)
                _settings = GetOrCreate();

            EditorGUILayout.HelpBox(
                "项目级设置。管线时机对所有 ClusterMesh / Skinned ClusterMesh 生效，不要写在单个 Renderer 上。\n" +
                "物体是否写出 Motion Vector 仍由各 Renderer 的 enableMotionVectors 决定；这里只决定写入时机。\n" +
                "本工程 Radiant GI 在 AfterRenderingSkybox + 2 读 _MotionVectorTexture，URP TAA 用同一张图。" +
                "必须先于它写入，Temporal 才认 ClusterMesh 在动。推荐默认 After Skybox + 1。",
                MessageType.Info);

            EditorGUILayout.LabelField("Motion Vector Pass", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "ClusterMesh 物体速度写入 URP Motion Vector 图的时机。勾选物体 MV 后，选对挂点 Radiant GI / TAA 才有效。",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterSkyboxPlus1, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterOpaques, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.AfterSkybox, _settings);
            DrawMotionVectorSlot(
                ClusterMeshMotionVectorSlot.BeforePostProcessingMinus1, _settings);
        }

        static void DrawMotionVectorSlot(ClusterMeshMotionVectorSlot slot, ClusterMeshSettings settings)
        {
            bool selected = settings.MotionVectorSlot == slot;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();
            bool next = EditorGUILayout.ToggleLeft(
                ClusterMeshUrpBridge.MotionVectorSlotLabel(slot), selected);
            if (EditorGUI.EndChangeCheck() && next && !selected)
            {
                Undo.RecordObject(settings, "ClusterMesh Motion Vector Pass");
                settings.MotionVectorSlot = slot;
                EditorUtility.SetDirty(settings);
            }

            EditorGUILayout.LabelField(
                ClusterMeshUrpBridge.MotionVectorSlotDescription(slot),
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }
    }
}
