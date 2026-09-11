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
            SerializedProperty evaluation = serializedObject.FindProperty("animationEvaluation");
            SerializedProperty parallelPrefix = serializedObject.FindProperty("enableParallelBonePrefix");
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

                if (prop.propertyPath == "animationEvaluation" ||
                    prop.propertyPath == "enableParallelBonePrefix")
                    continue;
                if (prop.propertyPath == "lodErrorThreshold")
                    DrawLodErrorThreshold(prop);
                else if (prop.propertyPath == "clipIndex")
                    DrawClipIndex(prop, renderer.asset);
                else
                    EditorGUILayout.PropertyField(prop, true);
            }

            DrawAnimationGroup(renderer, evaluation, parallelPrefix);

            serializedObject.ApplyModifiedProperties();
            renderer.ConstrainAnimationEvaluation();
            if (renderer.asset != null && renderer.asset.clips != null && renderer.asset.clips.Length > 0)
            {
                int clip = Mathf.Clamp(renderer.clipIndex, 0, renderer.asset.clips.Length - 1);
                bool gpuRequested = renderer.animationEvaluation == ClusterSkinnedAnimationEvaluation.GpuTexture;
                bool gpuReady = renderer.asset.HasGpuPalette(clip) &&
                    SystemInfo.SupportsTextureFormat(renderer.asset.gpuPaletteTextures[clip].format);
                bool cpuReady = renderer.asset.HasCpuBurstCurves(clip) &&
                    SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat);
                if (gpuRequested && !gpuReady)
                {
                    EditorGUILayout.HelpBox(
                        renderer.asset.animationDataMode == ClusterSkinnedAnimationDataMode.GpuAndCpu && cpuReady
                            ? "当前 GPU VTF 数据或纹理格式不可用，将回退到已烘焙的 CPU Hermite 数据。"
                            : "当前资产无法使用 GPU VTF 数据。该数据模式没有 CPU 回退，Renderer 将不会绘制；请用支持的设备运行或重新选择数据模式 Baker。",
                        MessageType.Error);
                }
                else if (!gpuRequested && !cpuReady)
                {
                    EditorGUILayout.HelpBox(
                        renderer.asset.animationDataMode == ClusterSkinnedAnimationDataMode.GpuAndCpu && gpuReady
                            ? "当前 CPU Hermite 数据不可用，将回退到已烘焙的 GPU VTF 数据。"
                            : "当前资产没有可用的 CPU Hermite 数据。该数据模式没有 GPU 回退，Renderer 将不会绘制；请重新 Baker。",
                        MessageType.Error);
                }
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

        static void DrawAnimationGroup(ClusterSkinnedMeshRenderer renderer, SerializedProperty evaluation,
            SerializedProperty parallelPrefix)
        {
            ClusterSkinnedMeshAsset asset = renderer.asset;
            ClusterSkinnedAnimationDataMode dataMode = asset != null
                ? asset.animationDataMode
                : ClusterSkinnedAnimationDataMode.GpuAndCpu;

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("动画运行优化", EditorStyles.boldLabel);
            if (asset != null)
                EditorGUILayout.LabelField("资产数据模式", dataMode.ToString());

            if (dataMode == ClusterSkinnedAnimationDataMode.GpuOnly)
            {
                evaluation.enumValueIndex = (int)ClusterSkinnedAnimationEvaluation.GpuTexture;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(evaluation, new GUIContent("Animation Evaluation"));
                EditorGUILayout.HelpBox(
                    "GPU Only 资产只能使用 VTF。优点是无 CPU 骨骼求值和 Palette 上传；缺点是 GPU 路径不可用时不会回退，也不会绘制。",
                    MessageType.None);
            }
            else if (dataMode == ClusterSkinnedAnimationDataMode.CpuOnly)
            {
                evaluation.enumValueIndex = (int)ClusterSkinnedAnimationEvaluation.CpuCurves;
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(evaluation, new GUIContent("Animation Evaluation"));
            }
            else
            {
                EditorGUILayout.PropertyField(evaluation, new GUIContent(
                    "Animation Evaluation",
                    "GPU + CPU 资产可选择首选路径；只有这种模式会在首选路径不可用时回退另一套数据。"));
                EditorGUILayout.HelpBox(
                    "GPU + CPU 同时包含两套数据：可运行时切换并允许回退，但资产体积和加载内存更大。",
                    MessageType.None);
            }

            bool includesCpu = dataMode != ClusterSkinnedAnimationDataMode.GpuOnly;
            if (includesCpu)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("CPU 优化", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(parallelPrefix, new GUIContent(
                    "前缀并行开启",
                    "仅 CPU Curves 实际生效。并行计算局部 Pose，再用树前缀积计算骨骼层级。深骨架或大批量实例可能受益；普通浅层人模会增加 Job 调度和临时缓冲成本。"));
                EditorGUILayout.HelpBox(
                    "优点：深骨架、大批量 CPU 动画时可提高并行度。缺点：增加 Job 次数和缓冲；普通人模可能更慢。默认关闭。",
                    MessageType.None);
            }

            if (dataMode != ClusterSkinnedAnimationDataMode.CpuOnly)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("GPU 优化", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "VTF Bake FPS 与纹理大小在 Baker 的“蒙皮动画优化”组设置。较低采样率节省包体、显存和带宽，较高采样率减少快速动画的插值误差。",
                    MessageType.None);
            }
            EditorGUILayout.EndVertical();
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
