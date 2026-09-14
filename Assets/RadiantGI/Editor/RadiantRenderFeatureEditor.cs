using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RadiantGI.Universal {

    [CustomEditor(typeof(RadiantRenderFeature))]
    public class RadiantRenderFeatureEditor : Editor {

        SerializedProperty renderingPath, ignorePostProcessingOption;
        SerializedProperty ignoreOverlayCameras, camerasLayerMask, useRenderingLayers;

        private void OnEnable() {
            renderingPath = serializedObject.FindProperty("renderingPath");
            ignorePostProcessingOption = serializedObject.FindProperty("ignorePostProcessingOption");
            ignoreOverlayCameras = serializedObject.FindProperty("ignoreOverlayCameras");
            camerasLayerMask = serializedObject.FindProperty("camerasLayerMask");
            useRenderingLayers = serializedObject.FindProperty("useRenderingLayers");
        }

        public override void OnInspectorGUI() {
            EditorGUILayout.PropertyField(ignorePostProcessingOption);
            EditorGUILayout.PropertyField(renderingPath);
            EditorGUILayout.PropertyField(ignoreOverlayCameras);
            EditorGUILayout.PropertyField(camerasLayerMask);
            EditorGUILayout.PropertyField(useRenderingLayers);
            if (useRenderingLayers.boolValue && !PipelineAssetSupportsRenderingLayers()) {
                EditorGUILayout.HelpBox("The active URP Pipeline Asset has \"Use Rendering Layers\" disabled (Lighting > Additional Properties). Without it the rendering layers texture is never written and the filter has no effect.", MessageType.Warning);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static bool PipelineAssetSupportsRenderingLayers() {
            UniversalRenderPipelineAsset urp = UniversalRenderPipeline.asset;
            return urp != null && urp.useRenderingLayers;
        }
    }
}
