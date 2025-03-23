using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    [CustomEditor(typeof(RPGController))]
    public class RPGControllerEditor : Editor {
        protected RPGController RpgController;
        protected RPGMotor RpgMotor;
        protected SerializedProperty Script;
        protected SerializedProperty ActivateControl;
        protected SerializedProperty UseLegacyInputSystem;
        protected SerializedProperty NormalizeInputValues;

        public virtual void OnEnable() {
            Script = serializedObject.FindProperty("m_Script");
            RpgController = (RPGController)serializedObject.targetObject;
            ActivateControl = serializedObject.FindProperty("ActivateControl");
            UseLegacyInputSystem = serializedObject.FindProperty("UseLegacyInputSystem");
            NormalizeInputValues = serializedObject.FindProperty("NormalizeInputValues");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            RpgMotor = RpgController?.GetComponent<RPGMotor>();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(Script);
            GUI.enabled = true;

            if (RpgMotor == null) {
                EditorGUILayout.LabelField("No RPGMotor component found!");
                EditorGUILayout.LabelField("Please assign one to use this RPGController");
                return;
            }

            EditorGUILayout.PropertyField(ActivateControl);
            if (!ActivateControl.boolValue) {
                EditorGUILayout.LabelField("└> Every player input is ignored");
            }
            EditorGUILayout.PropertyField(UseLegacyInputSystem);
            if (GUILayout.Button("Check input setup")) {
                Debug.Log("Input setup check started");
                RpgController.InitializeInputActions(true);
                Debug.Log("Input setup check done");
            }
            EditorGUILayout.PropertyField(NormalizeInputValues);

            serializedObject.ApplyModifiedProperties();
        }
    }
}