using JohnStairs.RCC.Character.Motor.Enums;
using UnityEditor;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    public class RPGMotorEditor : Editor {
        protected static bool showMovementSpeedSettings = true;
        protected static bool showJumpingAndMidairSettings = true;
        protected static bool showClimbingSettings = true;
        protected static bool showCameraInteractionSettings = true;
        protected static bool showPhysicsSettings = true;
        protected static bool showMiscSettings = true;

        #region Movement speed variables 
        SerializedProperty RunSpeed;
        SerializedProperty SprintSpeedMultiplier;
        SerializedProperty BackwardsSpeedMultiplier;
        SerializedProperty SwimSpeedMultiplier;
        SerializedProperty StrafeSpeed;
        SerializedProperty WalkSpeed;
        SerializedProperty CrouchSpeed;
        SerializedProperty SteadyAcceleration;
        SerializedProperty Acceleration;
        SerializedProperty AccelerationTime;
        SerializedProperty RotationSpeed;
        #endregion

        #region Jumping and midair variables
        SerializedProperty JumpHeight;
        SerializedProperty EnableMidairJumps;
        SerializedProperty RewardPerfectMidairJump;
        SerializedProperty PerfectDoubleJumpMultiplier;
        SerializedProperty JumpPeakTolerance;
        SerializedProperty DrawGizmoDuringJumpPeak;
        SerializedProperty LogApproxJumpPeakDuration;
        SerializedProperty AllowedMidairJumps;
        SerializedProperty EnableMidairMovement;
        SerializedProperty MidairSpeed;
        SerializedProperty UnlimitedMidairMoves;
        SerializedProperty AllowedMidairMoves;
        SerializedProperty EnableSwimmingJumps;
        #endregion

        #region Climbing variables
        SerializedProperty EnableLedgeClimbing;
        SerializedProperty LedgeClimbingUpSpeed;
        SerializedProperty LedgeGrabHeight;
        SerializedProperty LedgeGrabDistance;
        SerializedProperty LedgeGrabMaxSlope;
        SerializedProperty LedgeGrabCooldown;
        SerializedProperty LedgeClimbingUpTimeout;
        SerializedProperty OnlyAllowLedgeGrabbingWhileFalling;
        SerializedProperty DrawLedgeCheckGizmos;

        SerializedProperty EnableFreeClimbing;
        SerializedProperty FreeClimbingHandsHeight;
        SerializedProperty FreeClimbingFeetHeight;
        SerializedProperty FreeClimbingMaxCornerAngle;
        SerializedProperty FreeClimbingCooldown;
        SerializedProperty OnlyAllowFreeClimbingWhileGrounded;
        SerializedProperty DrawFreeClimbingCheckGizmos;

        SerializedProperty ClimbingSpeed;
        SerializedProperty GrabRange;
        SerializedProperty ClimbingCheckRadius;
        SerializedProperty ClimbableLayers;
        #endregion

        #region Camera interaction variables
        protected SerializedProperty AlignWithCamera;
        protected SerializedProperty AlsoRotateCamera;
        protected SerializedProperty AlignmentTime;
        #endregion

        #region Physics variables
        SerializedProperty IgnoredLayers;
        SerializedProperty EnableSliding;
        SerializedProperty SlidingTimeout;
        SerializedProperty AntiStuckTimeout;
        SerializedProperty EnableCollisionMovement;
        SerializedProperty GroundedTolerance;
        SerializedProperty FallingThreshold;
        SerializedProperty Gravity;
        #endregion

        #region Misc variables        
        SerializedProperty SwimmingStartHeight;
        SerializedProperty MoveWithMovingGround;
        SerializedProperty GroundAffectsJumping;
        SerializedProperty FlyingTimeout;
        #endregion

        public virtual void OnEnable() {
            #region Movement speed variables 
            RunSpeed = serializedObject.FindProperty("RunSpeed");
            SprintSpeedMultiplier = serializedObject.FindProperty("SprintSpeedMultiplier");
            BackwardsSpeedMultiplier = serializedObject.FindProperty("BackwardsSpeedMultiplier");
            SwimSpeedMultiplier = serializedObject.FindProperty("SwimSpeedMultiplier");
            StrafeSpeed = serializedObject.FindProperty("StrafeSpeed");
            WalkSpeed = serializedObject.FindProperty("WalkSpeed");
            CrouchSpeed = serializedObject.FindProperty("CrouchSpeed");
            SteadyAcceleration = serializedObject.FindProperty("SteadyAcceleration");
            Acceleration = serializedObject.FindProperty("Acceleration");
            AccelerationTime = serializedObject.FindProperty("AccelerationTime");
            RotationSpeed = serializedObject.FindProperty("RotationSpeed");
            #endregion
            #region Jumping and midair variables
            JumpHeight = serializedObject.FindProperty("JumpHeight");
            EnableMidairJumps = serializedObject.FindProperty("EnableMidairJumps");
            AllowedMidairJumps = serializedObject.FindProperty("AllowedMidairJumps");
            RewardPerfectMidairJump = serializedObject.FindProperty("RewardPerfectMidairJump");
            PerfectDoubleJumpMultiplier = serializedObject.FindProperty("PerfectDoubleJumpMultiplier");
            JumpPeakTolerance = serializedObject.FindProperty("JumpPeakTolerance");
            DrawGizmoDuringJumpPeak = serializedObject.FindProperty("DrawGizmoDuringJumpPeak");
            LogApproxJumpPeakDuration = serializedObject.FindProperty("LogApproxJumpPeakDuration");
            EnableMidairMovement = serializedObject.FindProperty("EnableMidairMovement");
            MidairSpeed = serializedObject.FindProperty("MidairSpeed");
            UnlimitedMidairMoves = serializedObject.FindProperty("UnlimitedMidairMoves");
            AllowedMidairMoves = serializedObject.FindProperty("AllowedMidairMoves");
            EnableSwimmingJumps = serializedObject.FindProperty("EnableSwimmingJumps");
            #endregion
            #region Climbing variables
            EnableLedgeClimbing = serializedObject.FindProperty("EnableLedgeClimbing");
            LedgeClimbingUpSpeed = serializedObject.FindProperty("LedgeClimbingUpSpeed");
            LedgeGrabHeight = serializedObject.FindProperty("LedgeGrabHeight");
            LedgeGrabDistance = serializedObject.FindProperty("LedgeGrabDistance");
            LedgeGrabCooldown = serializedObject.FindProperty("LedgeGrabCooldown");
            LedgeGrabMaxSlope = serializedObject.FindProperty("LedgeGrabMaxSlope");
            LedgeClimbingUpTimeout = serializedObject.FindProperty("LedgeClimbingUpTimeout");
            OnlyAllowLedgeGrabbingWhileFalling = serializedObject.FindProperty("OnlyAllowLedgeGrabbingWhileFalling");
            DrawLedgeCheckGizmos = serializedObject.FindProperty("DrawLedgeCheckGizmos");

            EnableFreeClimbing = serializedObject.FindProperty("EnableFreeClimbing");
            FreeClimbingHandsHeight = serializedObject.FindProperty("FreeClimbingHandsHeight");
            FreeClimbingFeetHeight = serializedObject.FindProperty("FreeClimbingFeetHeight");
            FreeClimbingMaxCornerAngle = serializedObject.FindProperty("FreeClimbingMaxCornerAngle");
            FreeClimbingCooldown = serializedObject.FindProperty("FreeClimbingCooldown");
            OnlyAllowFreeClimbingWhileGrounded = serializedObject.FindProperty("OnlyAllowFreeClimbingWhileGrounded");
            DrawFreeClimbingCheckGizmos = serializedObject.FindProperty("DrawFreeClimbingCheckGizmos");

            ClimbingSpeed = serializedObject.FindProperty("ClimbingSpeed");
            GrabRange = serializedObject.FindProperty("GrabRange");
            ClimbingCheckRadius = serializedObject.FindProperty("ClimbingCheckRadius");
            ClimbableLayers = serializedObject.FindProperty("ClimbableLayers");
            #endregion
            #region Camera interaction variables
            AlignWithCamera = serializedObject.FindProperty("AlignWithCamera");
            AlsoRotateCamera = serializedObject.FindProperty("AlsoRotateCamera");
            AlignmentTime = serializedObject.FindProperty("AlignmentTime");
            #endregion
            #region Physics variables
            IgnoredLayers = serializedObject.FindProperty("IgnoredLayers");
            EnableSliding = serializedObject.FindProperty("EnableSliding");
            SlidingTimeout = serializedObject.FindProperty("SlidingTimeout");
            AntiStuckTimeout = serializedObject.FindProperty("AntiStuckTimeout");
            EnableCollisionMovement = serializedObject.FindProperty("EnableCollisionMovement");
            GroundedTolerance = serializedObject.FindProperty("GroundedTolerance");
            FallingThreshold = serializedObject.FindProperty("FallingThreshold");
            Gravity = serializedObject.FindProperty("Gravity");
            #endregion
            #region Misc variables
            SwimmingStartHeight = serializedObject.FindProperty("SwimmingStartHeight");
            MoveWithMovingGround = serializedObject.FindProperty("MoveWithMovingGround");
            GroundAffectsJumping = serializedObject.FindProperty("GroundAffectsJumping");
            FlyingTimeout = serializedObject.FindProperty("FlyingTimeout");
            #endregion
        }

        protected virtual void PrintSettings(bool hideRotationSpeed = false) {
            #region Movement speed variables
            showMovementSpeedSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showMovementSpeedSettings, "Movement speed");
            if (showMovementSpeedSettings) {
                EditorGUILayout.PropertyField(RunSpeed);
                EditorGUILayout.PropertyField(SprintSpeedMultiplier, new GUIContent("└ Sprint Speed Multiplier "));
                EditorGUILayout.PropertyField(BackwardsSpeedMultiplier, new GUIContent("└ Backwards Speed Multiplier "));
                EditorGUILayout.PropertyField(SwimSpeedMultiplier, new GUIContent("└ Swim Speed Multiplier "));
                EditorGUILayout.PropertyField(StrafeSpeed);
                EditorGUILayout.PropertyField(WalkSpeed);
                EditorGUILayout.PropertyField(CrouchSpeed);
                EditorGUILayout.PropertyField(SteadyAcceleration);
                if (SteadyAcceleration.boolValue) {
                    EditorGUILayout.PropertyField(Acceleration, new GUIContent("└ Acceleration "));
                } else {
                    EditorGUILayout.PropertyField(AccelerationTime, new GUIContent("└ Acceleration Time "));
                }
                if (!hideRotationSpeed) {
                    EditorGUILayout.PropertyField(RotationSpeed);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Jumping and midair variables
            showJumpingAndMidairSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showJumpingAndMidairSettings, "Jumping and midair");
            if (showJumpingAndMidairSettings) {
                EditorGUILayout.PropertyField(JumpHeight);
                EditorGUILayout.PropertyField(EnableMidairJumps);
                if (EnableMidairJumps.boolValue) {
                    EditorGUILayout.PropertyField(AllowedMidairJumps, new GUIContent("└ Allowed Midair Jumps "));
                    EditorGUILayout.PropertyField(RewardPerfectMidairJump, new GUIContent("└ Reward Perfect Midair Jumps "));
                    if (RewardPerfectMidairJump.boolValue) {
                        EditorGUILayout.PropertyField(PerfectDoubleJumpMultiplier, new GUIContent("└ Perfect Jump Height Multiplier "));
                        EditorGUILayout.PropertyField(JumpPeakTolerance, new GUIContent("└ Jump Peak Tolerance "));
                        EditorGUILayout.PropertyField(DrawGizmoDuringJumpPeak, new GUIContent("└ Draw Gizmo During Jump Peak "));
                        EditorGUILayout.PropertyField(LogApproxJumpPeakDuration, new GUIContent("└ Log Approx Jump Peak Duration "));
                    }
                }
                EditorGUILayout.PropertyField(EnableMidairMovement);
                if (EnableMidairMovement.enumValueIndex != (int)MidairMovement.Never) {
                    EditorGUILayout.PropertyField(MidairSpeed, new GUIContent("└ Midair Speed "));
                    EditorGUILayout.PropertyField(UnlimitedMidairMoves, new GUIContent("└ Unlimited Midair Moves "));
                    if (!UnlimitedMidairMoves.boolValue) {
                        EditorGUILayout.PropertyField(AllowedMidairMoves, new GUIContent("   └ Allowed Midair Moves "));
                    }
                }
                EditorGUILayout.PropertyField(EnableSwimmingJumps);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion

            #region Climbing variables
            showClimbingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showClimbingSettings, "Climbing");
            if (showClimbingSettings) {
                EditorGUILayout.PropertyField(EnableLedgeClimbing);
                if (EnableLedgeClimbing.boolValue) {
                    EditorGUILayout.PropertyField(LedgeClimbingUpSpeed, new GUIContent("└ Climbing Up Speed "));
                    EditorGUILayout.PropertyField(LedgeGrabHeight, new GUIContent("└ Ledge Grab Height "));
                    EditorGUILayout.PropertyField(LedgeGrabDistance, new GUIContent("└ Ledge Grab Distance "));
                    EditorGUILayout.Slider(LedgeGrabMaxSlope, 5.0f, 85.0f, new GUIContent("└ Max Ledge Slope "));
                    EditorGUILayout.PropertyField(LedgeGrabCooldown, new GUIContent("└ Cooldown "));
                    EditorGUILayout.PropertyField(OnlyAllowLedgeGrabbingWhileFalling, new GUIContent("└ Only Allow While Falling "));
                    EditorGUILayout.PropertyField(LedgeClimbingUpTimeout, new GUIContent("└ Climbing Up Stuck Timeout "));
                    EditorGUILayout.PropertyField(DrawLedgeCheckGizmos, new GUIContent("└ Draw Check Gizmos "));
                }

                EditorGUILayout.PropertyField(EnableFreeClimbing);
                if (EnableFreeClimbing.boolValue) {
                    EditorGUILayout.PropertyField(FreeClimbingHandsHeight, new GUIContent("└ Hands Height "));
                    EditorGUILayout.PropertyField(FreeClimbingFeetHeight, new GUIContent("└ Feet Height "));
                    EditorGUILayout.Slider(FreeClimbingMaxCornerAngle, 20.0f, 85.0f, new GUIContent("└ Max Corner Angle "));
                    EditorGUILayout.PropertyField(FreeClimbingCooldown, new GUIContent("└ Cooldown "));
                    EditorGUILayout.PropertyField(OnlyAllowFreeClimbingWhileGrounded, new GUIContent("└ Only Allow While Grounded "));
                    EditorGUILayout.PropertyField(DrawFreeClimbingCheckGizmos, new GUIContent("└ Draw Check Gizmos "));
                }

                if (EnableLedgeClimbing.boolValue || EnableFreeClimbing.boolValue) {
                    EditorGUILayout.PropertyField(ClimbingSpeed);
                    EditorGUILayout.PropertyField(GrabRange);
                    EditorGUILayout.PropertyField(ClimbingCheckRadius, new GUIContent("Check Radius "));
                    EditorGUILayout.PropertyField(ClimbableLayers);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            #endregion
        }

        protected virtual void PrintPhysicsSettings() {
            showPhysicsSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showPhysicsSettings, "Physics");
            if (showPhysicsSettings) {
                EditorGUILayout.PropertyField(IgnoredLayers);
                EditorGUILayout.PropertyField(EnableSliding);
                if (EnableSliding.boolValue) {
                    EditorGUILayout.PropertyField(SlidingTimeout, new GUIContent("└ Sliding Timeout "));
                }
                EditorGUILayout.PropertyField(GroundedTolerance);
                EditorGUILayout.PropertyField(AntiStuckTimeout);
                EditorGUILayout.PropertyField(EnableCollisionMovement);
                EditorGUILayout.PropertyField(FallingThreshold);
                EditorGUILayout.PropertyField(Gravity);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        protected virtual void PrintMiscSettings() {
            EditorGUILayout.PropertyField(SwimmingStartHeight);
            EditorGUILayout.PropertyField(MoveWithMovingGround);
            if (MoveWithMovingGround.boolValue) {
                EditorGUILayout.PropertyField(GroundAffectsJumping, new GUIContent("└ Ground Affects Jumping "));
            }
            EditorGUILayout.PropertyField(FlyingTimeout);
        }
    }
}