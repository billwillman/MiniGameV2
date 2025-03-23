using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    [CustomEditor(typeof(RPGMotorMMO))]
    public class RPGMotorMMOEditor : RPGMotorEditor {
        SerializedProperty Script;
        SerializedProperty CameraControlled3dMovement;

        public override void OnEnable() {
            base.OnEnable();
            Script = serializedObject.FindProperty("m_Script");
            CameraControlled3dMovement = serializedObject.FindProperty("CameraControlled3dMovement");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(Script);
            GUI.enabled = true;

            base.PrintSettings();

            #region Camera interaction variables
            showCameraInteractionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCameraInteractionSettings, "Camera interaction");
            if (showCameraInteractionSettings) {
                EditorGUILayout.PropertyField(AlignWithCamera);
                EditorGUILayout.PropertyField(AlsoRotateCamera);
                EditorGUILayout.PropertyField(AlignmentTime);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            base.PrintPhysicsSettings();

            #region Misc variables
            showMiscSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showMiscSettings, "Miscellaneous");
            if (showMiscSettings) {
                base.PrintMiscSettings();
                EditorGUILayout.PropertyField(CameraControlled3dMovement);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            serializedObject.ApplyModifiedProperties();
        }
    }
}