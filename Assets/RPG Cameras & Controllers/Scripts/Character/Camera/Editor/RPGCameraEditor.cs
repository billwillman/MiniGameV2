using JohnStairs.RCC.Character.Cam.Enums;
using JohnStairs.RCC.Character.Motor;
using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Cam {
    [CustomEditor(typeof(RPGCamera))]
    public class RPGCameraEditor : Editor {
        static bool showGeneralSettings = true;
        static bool showRotationXSettings = true;
        static bool showRotationYSettings = true;
        static bool showDistanceSettings = true;
        static bool showCursorSettings = true;
        static bool showAlignmentSettings = true;
        static bool showShakingSettings = true;
        static bool showWaterInteractionSettings = true;

        RPGCamera RpgCamera;
        SerializedProperty Script;
        #region General variables
        SerializedProperty UseLegacyInputSystem;
        SerializedProperty CameraToUse;
        SerializedProperty UsedCamera;
        SerializedProperty ViewportMargin;
        SerializedProperty CameraPivotLocalPosition;
        SerializedProperty EnableIntelligentPivot;
        SerializedProperty IntelligentPivotSmoothTime;
        SerializedProperty PivotSmoothTime;
        SerializedProperty ActivateControl;
        SerializedProperty AlwaysActivateOrbiting;
        SerializedProperty RotationSmoothTime;
        SerializedProperty FollowBehavior;
        SerializedProperty UsedSkybox;
        #endregion

        #region Rotation X variables
        SerializedProperty StartRotationX;
        SerializedProperty StartRotationRelativeToCharacterRotation;
        SerializedProperty LockRotationX;
        SerializedProperty InvertRotationX;
        SerializedProperty RotationXSensitivity;
        SerializedProperty ConstrainRotationX;
        SerializedProperty RotationXMin;
        SerializedProperty RotationXMax;
        #endregion

        #region Rotation Y variables
        SerializedProperty StartRotationY;
        SerializedProperty LockRotationY;
        SerializedProperty InvertRotationY;
        SerializedProperty RotationYSensitivity;
        SerializedProperty RotationYMin;
        SerializedProperty RotationYMax;
        #endregion

        #region Distance variables
        SerializedProperty StartDistance;
        SerializedProperty StartWithZoomOut;
        SerializedProperty ZoomSensitivity;
        SerializedProperty MinDistance;
        SerializedProperty MaxDistance;
        SerializedProperty DistanceSmoothTime;
        #endregion

        #region Cursor variables
        SerializedProperty HideCursor;
        SerializedProperty CursorBehaviorOrbiting;
        #endregion

        #region Alignment variables
        SerializedProperty AlignWhenMoving;
        SerializedProperty OrbitingPausesCharacterAlignment;
        SerializedProperty AlignOnlyIfLockedOnTarget;
        SerializedProperty AlignmentSmoothTime;
        SerializedProperty SupportMovingBackwards;
        #endregion

        #region Shaking variables
        SerializedProperty ShakeFrequency;
        SerializedProperty ShakeAmplitude;
        SerializedProperty ShakeAmplitudeVariance;
        #endregion

        #region Water interaction variables
        SerializedProperty SkipWaterSurface;
        SerializedProperty EnableUnderwaterEffect;
        SerializedProperty UnderwaterFogColor;
        SerializedProperty UnderwaterFogDensity;
        SerializedProperty UnderwaterThresholdTuning;
        #endregion

        public void OnEnable() {
            Script = serializedObject.FindProperty("m_Script");
            RpgCamera = (RPGCamera)serializedObject.targetObject;
            #region General variables
            UseLegacyInputSystem = serializedObject.FindProperty("UseLegacyInputSystem");
            CameraToUse = serializedObject.FindProperty("CameraToUse");
            UsedCamera = serializedObject.FindProperty("UsedCamera");
            ViewportMargin = serializedObject.FindProperty("ViewportMargin");
            CameraPivotLocalPosition = serializedObject.FindProperty("CameraPivotLocalPosition");
            EnableIntelligentPivot = serializedObject.FindProperty("EnableIntelligentPivot");
            IntelligentPivotSmoothTime = serializedObject.FindProperty("IntelligentPivotSmoothTime");
            PivotSmoothTime = serializedObject.FindProperty("PivotSmoothTime");
            ActivateControl = serializedObject.FindProperty("ActivateControl");
            AlwaysActivateOrbiting = serializedObject.FindProperty("AlwaysActivateOrbiting");
            RotationSmoothTime = serializedObject.FindProperty("RotationSmoothTime");
            FollowBehavior = serializedObject.FindProperty("FollowBehavior");
            UsedSkybox = serializedObject.FindProperty("UsedSkybox");
            #endregion
            #region Rotation X variables
            StartRotationX = serializedObject.FindProperty("StartRotationX");
            StartRotationRelativeToCharacterRotation = serializedObject.FindProperty("StartRotationRelativeToCharacterRotation");
            LockRotationX = serializedObject.FindProperty("LockRotationX");
            InvertRotationX = serializedObject.FindProperty("InvertRotationX");
            RotationXSensitivity = serializedObject.FindProperty("RotationXSensitivity");
            ConstrainRotationX = serializedObject.FindProperty("ConstrainRotationX");
            RotationXMin = serializedObject.FindProperty("RotationXMin");
            RotationXMax = serializedObject.FindProperty("RotationXMax");
            #endregion
            #region Rotation Y variables
            StartRotationY = serializedObject.FindProperty("StartRotationY");
            LockRotationY = serializedObject.FindProperty("LockRotationY");
            InvertRotationY = serializedObject.FindProperty("InvertRotationY");
            RotationYSensitivity = serializedObject.FindProperty("RotationYSensitivity");
            RotationYMin = serializedObject.FindProperty("RotationYMin");
            RotationYMax = serializedObject.FindProperty("RotationYMax");
            #endregion
            #region Distance variables
            StartDistance = serializedObject.FindProperty("StartDistance");
            StartWithZoomOut = serializedObject.FindProperty("StartWithZoomOut");
            ZoomSensitivity = serializedObject.FindProperty("ZoomSensitivity");
            MinDistance = serializedObject.FindProperty("MinDistance");
            MaxDistance = serializedObject.FindProperty("MaxDistance");
            DistanceSmoothTime = serializedObject.FindProperty("DistanceSmoothTime");
            #endregion
            #region Cursor variables
            HideCursor = serializedObject.FindProperty("HideCursor");
            CursorBehaviorOrbiting = serializedObject.FindProperty("CursorBehaviorOrbiting");
            #endregion
            #region Alignment variables
            AlignWhenMoving = serializedObject.FindProperty("AlignWhenMoving");
            OrbitingPausesCharacterAlignment = serializedObject.FindProperty("OrbitingPausesCharacterAlignment");
            AlignOnlyIfLockedOnTarget = serializedObject.FindProperty("AlignOnlyIfLockedOnTarget");
            AlignmentSmoothTime = serializedObject.FindProperty("AlignmentSmoothTime");
            SupportMovingBackwards = serializedObject.FindProperty("SupportMovingBackwards");
            #endregion
            #region Shaking variables
            ShakeFrequency = serializedObject.FindProperty("ShakeFrequency");
            ShakeAmplitude = serializedObject.FindProperty("ShakeAmplitude");
            ShakeAmplitudeVariance = serializedObject.FindProperty("ShakeAmplitudeVariance");
            #endregion
            #region Water interaction variables
            SkipWaterSurface = serializedObject.FindProperty("SkipWaterSurface");
            EnableUnderwaterEffect = serializedObject.FindProperty("EnableUnderwaterEffect");
            UnderwaterFogColor = serializedObject.FindProperty("UnderwaterFogColor");
            UnderwaterFogDensity = serializedObject.FindProperty("UnderwaterFogDensity");
            UnderwaterThresholdTuning = serializedObject.FindProperty("UnderwaterThresholdTuning");
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
                EditorGUILayout.PropertyField(ActivateControl);
                if (!ActivateControl.boolValue) {
                    EditorGUILayout.LabelField("└> Every player input is ignored");
                }
                EditorGUILayout.PropertyField(UseLegacyInputSystem);
                if (GUILayout.Button("Check input setup")) {
                    Debug.Log("Input setup check started");
                    RpgCamera.InitializeInputActions(true);
                    Debug.Log("Input setup check done");
                }
                EditorGUILayout.PropertyField(CameraToUse);
                if (CameraToUse.enumValueIndex == (int)CameraSelection.AssignedCamera) {
                    EditorGUILayout.PropertyField(UsedCamera);
                }
                EditorGUILayout.PropertyField(ViewportMargin);
                EditorGUILayout.PropertyField(CameraPivotLocalPosition);
                bool internalPivot = RpgCamera.HasInternalPivot();
                EditorGUILayout.LabelField("└> " + (internalPivot ? "Internal pivot logic applies" : "External pivot logic applies"));
                GUI.enabled = internalPivot;
                EditorGUILayout.PropertyField(EnableIntelligentPivot, new GUIContent("└ Enable Intelligent Pivot "));
                if (EnableIntelligentPivot.boolValue) {
                    EditorGUILayout.PropertyField(IntelligentPivotSmoothTime, new GUIContent("   └ Smooth Time "));
                }
                GUI.enabled = true;
                EditorGUILayout.PropertyField(PivotSmoothTime, new GUIContent("└ Pivot Smooth Time "));
                EditorGUILayout.PropertyField(AlwaysActivateOrbiting);
                EditorGUILayout.PropertyField(RotationSmoothTime);
                EditorGUILayout.PropertyField(FollowBehavior);
                if (FollowBehavior.enumValueIndex == (int)Follow.Lazy) {
                    EditorGUILayout.LabelField("└> Some settings below only apply when locked on target");
                }
                EditorGUILayout.PropertyField(UsedSkybox);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Rotation X variables
            showRotationXSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showRotationXSettings, "Rotation X");
            if (showRotationXSettings) {
                EditorGUILayout.PropertyField(StartRotationX);
                EditorGUILayout.PropertyField(StartRotationRelativeToCharacterRotation, new GUIContent("└ Relative To Character Rotation "));
                EditorGUILayout.PropertyField(LockRotationX);
                EditorGUILayout.PropertyField(InvertRotationX);
                EditorGUILayout.PropertyField(RotationXSensitivity);
                EditorGUILayout.PropertyField(ConstrainRotationX);
                if (ConstrainRotationX.boolValue) {
                    EditorGUILayout.PropertyField(RotationXMin, new GUIContent("└ Min Degrees "));
                    EditorGUILayout.PropertyField(RotationXMax, new GUIContent("└ Max Degrees "));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Rotation Y variables
            showRotationYSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showRotationYSettings, "Rotation Y");
            if (showRotationYSettings) {
                EditorGUILayout.PropertyField(StartRotationY);
                EditorGUILayout.PropertyField(LockRotationY);
                EditorGUILayout.PropertyField(InvertRotationY);
                EditorGUILayout.PropertyField(RotationYSensitivity);
                EditorGUILayout.PropertyField(RotationYMin);
                EditorGUILayout.PropertyField(RotationYMax);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Distance variables
            showDistanceSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showDistanceSettings, "Distance");
            if (showDistanceSettings) {
                EditorGUILayout.PropertyField(StartDistance);
                EditorGUILayout.PropertyField(StartWithZoomOut);
                EditorGUILayout.PropertyField(ZoomSensitivity);
                EditorGUILayout.PropertyField(MinDistance);
                EditorGUILayout.PropertyField(MaxDistance);
                EditorGUILayout.PropertyField(DistanceSmoothTime);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Cursor variables
            showCursorSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showCursorSettings, "Cursor");
            if (showCursorSettings) {
                EditorGUILayout.PropertyField(HideCursor);
                EditorGUILayout.PropertyField(CursorBehaviorOrbiting, new GUIContent("Behavior While Orbiting"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Character alignment variables
            showAlignmentSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showAlignmentSettings, "Alignment when the character moves");
            if (showAlignmentSettings) {
                EditorGUILayout.PropertyField(AlignWhenMoving, new GUIContent("Align With Character "));
                if (AlignWhenMoving.boolValue) {
                    EditorGUILayout.PropertyField(OrbitingPausesCharacterAlignment, new GUIContent("└ Pause While Orbiting "));
                    if (RpgCamera.GetComponent<RPGMotorARPG>() != null) {
                        // In combination with ARPG Motor => lock setting
                        GUI.enabled = false;
                        AlignOnlyIfLockedOnTarget.boolValue = true;
                        EditorGUILayout.PropertyField(AlignOnlyIfLockedOnTarget, new GUIContent("└ Only If Locked On Target "));
                        GUI.enabled = true;
                    } else {
                        // Other motor
                        EditorGUILayout.PropertyField(AlignOnlyIfLockedOnTarget, new GUIContent("└ Only If Locked On Target "));
                    }
                    EditorGUILayout.PropertyField(AlignmentSmoothTime, new GUIContent("└ Alignment Smooth Time "));
                    EditorGUILayout.PropertyField(SupportMovingBackwards, new GUIContent("└ Support Moving Backwards "));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Shaking variables
            showShakingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showShakingSettings, "Shaking simulation");
            if (showShakingSettings) {
                EditorGUILayout.PropertyField(ShakeFrequency, new GUIContent("Frequency "));
                EditorGUILayout.PropertyField(ShakeAmplitude, new GUIContent("Amplitude "));
                EditorGUILayout.PropertyField(ShakeAmplitudeVariance, new GUIContent("Amplitude Variance "));
                string buttonText = "Enter play mode first";
                if (Application.isPlaying) {
                    buttonText = RpgCamera.IsShaking() ? "Stop simulation" : "Start simulation";
                }
                if (GUILayout.Button(buttonText)) {
                    ToggleShakingSimulation(RpgCamera);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Water interaction variables
            showWaterInteractionSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showWaterInteractionSettings, "Water interaction");
            if (showWaterInteractionSettings) {
                EditorGUILayout.PropertyField(SkipWaterSurface);
                EditorGUILayout.PropertyField(EnableUnderwaterEffect);
                if (EnableUnderwaterEffect.boolValue) {
                    EditorGUILayout.PropertyField(UnderwaterFogColor, new GUIContent("└ Fog Color "));
                    EditorGUILayout.PropertyField(UnderwaterFogDensity, new GUIContent("└ Fog Density "));
                    EditorGUILayout.PropertyField(UnderwaterThresholdTuning, new GUIContent("└ Threshold Tuning "));
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void ToggleShakingSimulation(RPGCamera rpgCamera) {
            if (!Application.isPlaying) {
                Debug.Log("You have to enter play mode first to be able to simulate camera shaking");
                return;
            }

            if (rpgCamera.IsShaking()) {
                rpgCamera.StopShaking();
            } else {
                rpgCamera.Shake(ShakeFrequency.floatValue, ShakeAmplitude.floatValue, ShakeAmplitudeVariance.floatValue);
            }
        }
    }
}