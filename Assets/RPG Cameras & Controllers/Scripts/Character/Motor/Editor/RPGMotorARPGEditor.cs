using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    [CustomEditor(typeof(RPGMotorARPG))]
    public class RPGMotorARPGEditor : RPGMotorEditor {
        SerializedProperty Script;
        SerializedProperty AlwaysTurnToCursor;
        SerializedProperty CompleteTurnWhileStanding;
        SerializedProperty StrafeWhenLockedOnTarget;

        public override void OnEnable() {
            base.OnEnable();
            Script = serializedObject.FindProperty("m_Script");
            AlwaysTurnToCursor = serializedObject.FindProperty("AlwaysTurnToCursor");
            CompleteTurnWhileStanding = serializedObject.FindProperty("CompleteTurnWhileStanding");
            StrafeWhenLockedOnTarget = serializedObject.FindProperty("StrafeWhenLockedOnTarget");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(Script);
            GUI.enabled = true;

            base.PrintSettings(true);

            #region Camera interaction variables
            showCameraInteractionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCameraInteractionSettings, "Camera interaction");
            if (showCameraInteractionSettings) {
                EditorGUILayout.PropertyField(AlwaysTurnToCursor);
                if (!AlwaysTurnToCursor.boolValue) {
                    EditorGUILayout.PropertyField(AlignWithCamera);
                }
                EditorGUILayout.PropertyField(AlignmentTime);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            base.PrintPhysicsSettings();

            #region Misc variables
            showMiscSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showMiscSettings, "Miscellaneous");
            if (showMiscSettings) {
                base.PrintMiscSettings();
                EditorGUILayout.PropertyField(CompleteTurnWhileStanding);
                EditorGUILayout.PropertyField(StrafeWhenLockedOnTarget);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            serializedObject.ApplyModifiedProperties();
        }
    }
}