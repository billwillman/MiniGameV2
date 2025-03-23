using JohnStairs.RCC.Character.Cam;
using JohnStairs.RCC.Character.Cam.Enums;
using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Cam {
    [CustomEditor(typeof(RPGViewFrustum))]
    public class RPGViewFrustumEditor : Editor {
        static bool showGeneralSettings = true;
        static bool showOcclusionSettings = true;
        static bool showCameraLookUpSettings = true;
        static bool showObjectFadingSettings = true;
        static bool showCharacterFadingSettings = true;

        SerializedProperty Script;
        #region General variables 
        SerializedProperty Shape;
        SerializedProperty RaysPerEdge;
        #endregion
        #region Occlusion variables 
        SerializedProperty OccludingLayers;
        SerializedProperty FadeObjectsBy;
        SerializedProperty _tagsForFading;
        SerializedProperty TagForFadeOut;
        SerializedProperty LayersForFading;
        #endregion
        #region Camera look up variables 
        SerializedProperty EnableCameraLookUp;
        SerializedProperty LookUpTrigger;
        SerializedProperty _tagsCausingLookUp;
        SerializedProperty TagCausingLookUp;
        SerializedProperty LayersCausingLookUp;
        #endregion
        #region Object fading variables 
        SerializedProperty FadeOutAlpha;
        SerializedProperty FadeInAlpha;
        SerializedProperty FadeOutDuration;
        SerializedProperty FadeInDuration;
        #endregion
        #region Character fading variables 
        SerializedProperty EnableCharacterFading;
        SerializedProperty CharacterFadeOutAlpha;
        SerializedProperty CharacterFadeStartDistance;
        SerializedProperty CharacterFadeEndDistance;
        #endregion

        public void OnEnable() {
            Script = serializedObject.FindProperty("m_Script");
            #region General variables
            Shape = serializedObject.FindProperty("Shape");
            RaysPerEdge = serializedObject.FindProperty("RaysPerEdge");
            #endregion
            #region Occlusion variables
            OccludingLayers = serializedObject.FindProperty("OccludingLayers");
            FadeObjectsBy = serializedObject.FindProperty("FadeObjectsBy");
            _tagsForFading = serializedObject.FindProperty("TagsForFading");
            LayersForFading = serializedObject.FindProperty("LayersForFading");
            #endregion
            #region Camera look up variables 
            EnableCameraLookUp = serializedObject.FindProperty("EnableCameraLookUp");
            LookUpTrigger = serializedObject.FindProperty("LookUpTrigger");
            _tagsCausingLookUp = serializedObject.FindProperty("TagsCausingLookUp");
            LayersCausingLookUp = serializedObject.FindProperty("LayersCausingLookUp");
            #endregion
            #region Object fading variables 
            FadeOutAlpha = serializedObject.FindProperty("FadeOutAlpha");
            FadeInAlpha = serializedObject.FindProperty("FadeInAlpha");
            FadeOutDuration = serializedObject.FindProperty("FadeOutDuration");
            FadeInDuration = serializedObject.FindProperty("FadeInDuration");
            #endregion
            #region Character fading variables
            EnableCharacterFading = serializedObject.FindProperty("EnableCharacterFading");
            CharacterFadeOutAlpha = serializedObject.FindProperty("CharacterFadeOutAlpha");
            CharacterFadeStartDistance = serializedObject.FindProperty("CharacterFadeStartDistance");
            CharacterFadeEndDistance = serializedObject.FindProperty("CharacterFadeEndDistance");
            #endregion
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            
            GUI.enabled = false;
            EditorGUILayout.PropertyField(Script);
            GUI.enabled = true;

            #region General variables
            showGeneralSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showGeneralSettings, "General");
            if (showGeneralSettings) {
                EditorGUILayout.PropertyField(Shape);
                if (Shape.enumValueIndex == (int)FrustumShape.Pyramid) {
                    EditorGUILayout.PropertyField(RaysPerEdge);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Occlusion variables
            showOcclusionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showOcclusionSettings, "Occlusion");
            if (showOcclusionSettings) {
                EditorGUILayout.PropertyField(OccludingLayers);
                EditorGUILayout.PropertyField(FadeObjectsBy);
                if (FadeObjectsBy.enumValueIndex == (int)ObjectTriggerOption.Tag) {
                    #region TagsForFadeOut
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(_tagsForFading.displayName);
                    int numberOfTagsForFading = EditorGUILayout.IntField(_tagsForFading.arraySize);
                    if (numberOfTagsForFading > 0
                        && numberOfTagsForFading != _tagsForFading.arraySize) {
                        // Resize the array
                        _tagsForFading.arraySize = numberOfTagsForFading;
                    }
                    EditorGUILayout.EndHorizontal();

                    for (int i = 0; i < _tagsForFading.arraySize; i++) {
                        TagForFadeOut = _tagsForFading.GetArrayElementAtIndex(i);
                        TagForFadeOut.stringValue = EditorGUILayout.TagField("└ Tag " + i, TagForFadeOut.stringValue);
                    }
                    #endregion
                } else if (LookUpTrigger.enumValueIndex == (int)ObjectTriggerOption.Layer) {
                    EditorGUILayout.PropertyField(LayersForFading);
                } else {
                    EditorGUILayout.LabelField("Component For Fading", "FadeOut");
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Camera look up variables
            showCameraLookUpSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCameraLookUpSettings, "Camera look up");
            if (showCameraLookUpSettings) {
                EditorGUILayout.PropertyField(EnableCameraLookUp);
                if (EnableCameraLookUp.boolValue) {
                    EditorGUILayout.PropertyField(LookUpTrigger);
                    if (LookUpTrigger.enumValueIndex == (int)ObjectTriggerOption.Tag) {
                        #region TagsForFadeOut
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PrefixLabel(_tagsCausingLookUp.displayName);
                        int numberOfTagsCausingLookUp = EditorGUILayout.IntField(_tagsCausingLookUp.arraySize);
                        if (numberOfTagsCausingLookUp > 0
                            && numberOfTagsCausingLookUp != _tagsCausingLookUp.arraySize) {
                            // Resize the array
                            _tagsCausingLookUp.arraySize = numberOfTagsCausingLookUp;
                        }
                        EditorGUILayout.EndHorizontal();

                        for (int i = 0; i < _tagsCausingLookUp.arraySize; i++) {
                            TagCausingLookUp = _tagsCausingLookUp.GetArrayElementAtIndex(i);
                            TagCausingLookUp.stringValue = EditorGUILayout.TagField("└ Tag " + i, TagCausingLookUp.stringValue);
                        }
                        #endregion
                    } else if (LookUpTrigger.enumValueIndex == (int)ObjectTriggerOption.Layer) {
                        EditorGUILayout.PropertyField(LayersCausingLookUp);
                    } else {
                        EditorGUILayout.LabelField("Comp. Causing Look Up", "CauseCameraLookUp");
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Object fading variables 
            showObjectFadingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showObjectFadingSettings, "Object fading");
            if (showObjectFadingSettings) {
                EditorGUILayout.PropertyField(FadeOutAlpha);
                EditorGUILayout.PropertyField(FadeInAlpha);
                EditorGUILayout.PropertyField(FadeOutDuration);
                EditorGUILayout.PropertyField(FadeInDuration);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Character fading variables
            showCharacterFadingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCharacterFadingSettings, "Character fading");
            if (showCharacterFadingSettings) {
                EditorGUILayout.PropertyField(EnableCharacterFading);
                if (EnableCharacterFading.boolValue) {
                    EditorGUILayout.PropertyField(CharacterFadeOutAlpha, new GUIContent("└ Fade Out Alpha "));
                    EditorGUILayout.PropertyField(CharacterFadeStartDistance, new GUIContent("└ Fade Start Distance "));
                    EditorGUILayout.PropertyField(CharacterFadeEndDistance, new GUIContent("└ Fade End Distance "));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            serializedObject.ApplyModifiedProperties();
        }
    }
}