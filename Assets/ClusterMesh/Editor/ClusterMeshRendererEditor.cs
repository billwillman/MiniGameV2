using UnityEditor;
using UnityEngine;

namespace ClusterMesh
{
    [CustomEditor(typeof(ClusterMeshRenderer))]
    public sealed class ClusterMeshRendererEditor : UnityEditor.Editor
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

                if (prop.propertyPath == "enableConeCull")
                    ClusterMeshRadiantPathInspector.DrawGroups(serializedObject);
            }

            serializedObject.ApplyModifiedProperties();
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

    static class ClusterMeshRadiantPathInspector
    {
        public static void DrawGroups(SerializedObject so)
        {
            if (so == null)
                return;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Radiant GI 公共", EditorStyles.boldLabel);
            SerializedProperty motion = so.FindProperty("enableMotionVectors");
            if (motion != null)
                EditorGUILayout.PropertyField(motion, true);

            EditorGUILayout.Space(2);
            if (ClusterMeshUrpBridge.IsForwardFeatureActive())
            {
                EditorGUILayout.LabelField("Radiant GI Forward", EditorStyles.boldLabel);
                SerializedProperty depthNormals = so.FindProperty("enableDepthNormals");
                if (depthNormals != null)
                    EditorGUILayout.PropertyField(depthNormals, true);
            }
            else if (ClusterMeshUrpBridge.IsDeferredFeatureActive())
            {
                EditorGUILayout.LabelField("Radiant GI 延迟", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "延迟读 GBuffer 法线，无 DepthNormals。Organic Light 在 Radiant Volume 上。",
                    MessageType.None);
            }
            EditorGUILayout.EndVertical();
        }
    }
}
