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
            var renderer = (ClusterSkinnedMeshRenderer)target;
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
                else if (prop.propertyPath == "clipIndex")
                    DrawClipIndex(prop, renderer.asset);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();
            if (renderer.animationEvaluation == ClusterSkinnedAnimationEvaluation.GpuTexture &&
                renderer.asset != null && renderer.asset.clips != null && renderer.asset.clips.Length > 0 &&
                !renderer.asset.HasGpuPalette(Mathf.Clamp(renderer.clipIndex, 0, renderer.asset.clips.Length - 1)))
            {
                EditorGUILayout.HelpBox(
                    "当前资产没有可用的 GPU Palette Atlas，将自动回退 CPU Curves。重新 Baker 后可启用真正的 GPU Texture 动画。",
                    MessageType.Warning);
            }
            if (renderer.animationEvaluation == ClusterSkinnedAnimationEvaluation.CpuCurves &&
                renderer.asset != null && renderer.asset.clips != null && renderer.asset.clips.Length > 0 &&
                !renderer.asset.HasCpuBurstCurves(Mathf.Clamp(renderer.clipIndex, 0, renderer.asset.clips.Length - 1)))
            {
                EditorGUILayout.HelpBox(
                    "当前资产没有可用的 Burst 曲线数据，将使用旧版兼容路径。重新 Baker 后，CPU Curves 会由 Job System + Burst 计算。",
                    MessageType.Warning);
            }
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

        static void DrawClipIndex(SerializedProperty prop, ClusterSkinnedMeshAsset asset)
        {
            ClusterSkinnedClip[] clips = asset != null ? asset.clips : null;
            if (clips == null || clips.Length == 0)
            {
                EditorGUILayout.PropertyField(prop, true);
                return;
            }

            var labels = new string[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                string name = clips[i] != null && !string.IsNullOrEmpty(clips[i].name)
                    ? clips[i].name : "<Missing>";
                labels[i] = i + ": " + name;
            }
            prop.intValue = EditorGUILayout.Popup(
                new GUIContent(prop.displayName, "选择 Baker 列表中对应序号的动画 Clip。"),
                Mathf.Clamp(prop.intValue, 0, clips.Length - 1), labels);
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
