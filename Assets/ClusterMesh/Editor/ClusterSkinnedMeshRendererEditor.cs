using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [CustomEditor(typeof(ClusterSkinnedMeshRenderer))]
    public sealed class ClusterSkinnedMeshRendererEditor : UnityEditor.Editor
    {
        const float PresetWidth = 80f;
        static readonly string[] CustomLabels = { "自定义", "极度精细", "精细", "一般", "粗糙" };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(prop, true);
                    continue;
                }

                if (prop.propertyPath == "lodErrorThreshold")
                    DrawLodErrorThreshold(prop);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
            var renderer = (ClusterSkinnedMeshRenderer)target;
            if (renderer.asset != null &&
                renderer.asset.animationSamplingVersion != ClusterSkinnedMeshAsset.CurrentAnimationSamplingVersion)
            {
                EditorGUILayout.HelpBox(
                    "该 Skinned ClusterMesh 使用旧的动画采样数据，请在 Baker 中重新烘焙后再播放。",
                    MessageType.Error);
                if (GUILayout.Button("打开 ClusterMesh Baker"))
                    ClusterMeshBakerWindow.Open();
            }
            if (renderer.playAutomatically)
                EditorGUILayout.HelpBox("自动播放开启时，Normalized Time 是播放相位偏移；需要手动定格拖动时请关闭 Play Automatically。", MessageType.Info);
        }

        static void DrawLodErrorThreshold(SerializedProperty prop)
        {
            Rect row = EditorGUILayout.GetControlRect();
            var popup = new Rect(row.xMax - PresetWidth, row.y, PresetWidth, row.height);
            var field = new Rect(row.x, row.y, row.width - PresetWidth - 4f, row.height);

            EditorGUI.PropertyField(field, prop, new GUIContent(prop.displayName, prop.tooltip));

            int current = ClusterMeshLodQuality.PopupIndex(prop.floatValue);
            bool custom = current < 0;
            string[] labels = custom ? CustomLabels : ClusterMeshLodQuality.Labels;
            int shown = custom ? 0 : current;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(popup, shown, labels);
            if (EditorGUI.EndChangeCheck())
            {
                int preset = custom ? next - 1 : next;
                if (preset >= 0)
                    prop.floatValue = ClusterMeshLodQuality.ValueFromPopupIndex(preset);
            }
        }
    }
}
