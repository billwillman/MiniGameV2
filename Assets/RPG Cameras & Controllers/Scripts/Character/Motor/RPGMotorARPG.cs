using JohnStairs.RCC.Character.Motor.Enums;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    public class RPGMotorARPG : RPGMotor {
        /// <summary>
        /// If set to true, the character always turns towards the cursor position
        /// </summary>
        [Tooltip("If set to true, the character always turns towards the cursor position.")]
        public bool AlwaysTurnToCursor = false;
        /// <summary>
        /// If set to true and while standing, the character begins moving forward after it completely turned into the new movement direction
        /// </summary>
        [Tooltip("If set to true and while standing, the character begins moving forward after it completely turned into the new movement direction.")]
        public bool CompleteTurnWhileStanding = true;
        /// <summary>
        /// If set to true and while locked on a target, the character will always strafe instead of turning into the movement direction
        /// </summary>
        [Tooltip("If set to true and while locked on a target, the character will always strafe instead of turning into the movement direction.")]
        public bool StrafeWhenLockedOnTarget = false;

        /// <summary>
        /// True if the character is already in motion
        /// </summary>
        protected bool _inMotionAlready;

        protected override Vector3 GetMovementDirection() {
            _inMotionAlready = IsInMotion();

            Vector3 input = _inputDirection;
            Vector3 right = Vector3.zero;
            Vector3 forward = Vector3.zero;
            Vector3 up = Vector3.zero;

            if (_strafe != 0) {
                input.x = _strafe;
            }

            Camera usedCamera = _rpgCamera?.GetUsedCamera();
            if (IsLockedOnTarget()) {
                forward = Utils.ProjectOnHorizontalPlane(GetTargetPosition() - transform.position);
                right = Vector3.Cross(Vector3.up, forward);
            } else if (usedCamera) {
                right = usedCamera.transform.right;
                forward = Vector3.Cross(right, Allow3dMovement() ? usedCamera.transform.up : Vector3.up);
            }

            if (_surface) {
                up = Vector3.up;
            } else if (_dive) {
                up = Vector3.down;
            }

            return Utils.ClampMagnitudeTo1(right * input.x + forward * input.z + up);
        }

        protected override Vector3 GetClimbingDirection() {
            Vector3 input = _inputDirection;
            Vector3 right = Vector3.zero;
            Vector3 forward = Vector3.zero;

            if (_strafe != 0) {
                input.x = _strafe;
            }

            Camera usedCamera = _rpgCamera?.GetUsedCamera();
            if (usedCamera) {
                forward = transform.forward;
                right = transform.right;

                if (Vector3.Angle(usedCamera.transform.forward, transform.forward) > 90.0f) {
                    forward *= -1.0f;
                    right *= -1.0f;
                }
            }

            return Utils.ClampMagnitudeTo1(right * input.x + forward * input.z);
        }

        protected override Vector3 GetFacingDirection() {
            Camera usedCamera = _rpgCamera?.GetUsedCamera();
            if (AlwaysTurnToCursor && usedCamera) {
                if (Physics.Raycast(usedCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit)) {
                    return TurnTowards(hit.point);
                } else {
                    return GetForwardInHorizontalPlane();
                }
            }

            if (IsStrafing()) {
                if (IsLockedOnTarget()) {
                    return TurnTowards(GetTargetPosition());
                } else {
                    return AlignWithCameraDirection();
                }
            }

            if (AlignWithCamera == CharacterAlignment.Always
                || AlignWithCamera == CharacterAlignment.OnAlignmentInput && _alignWithCamera) {
                return AlignWithCameraDirection();
            }

            Vector3 facingDirection = _movementDirection;
            if (!_canMove) {
                // _movementDirection was not determined yet
                facingDirection = GetMovementDirection();
            }

            if (facingDirection == Vector3.zero) {
                // Look forward in horizontal plane
                return GetForwardInHorizontalPlane();
            } else if (IsCloseToWaterSurface()) {
                return Vector3.ProjectOnPlane(-transform.up, Vector3.up);
            } else {
                // Keep the current facing direction
                return facingDirection;
            }
        }

        protected override float GetStandardMovementSpeed() {
            if (!_canRotate) {
                // As ARPG character movement depends on the camera view direction, being not able to rotate breaks it
                return 0;
            }

            // Check if the character should turn first towards the facing direction before moving
            if (CompleteTurnWhileStanding
                && !_inMotionAlready // to prevent stopping and turning while already in motion
                && !IsLookingInDirection(_facingDirection)) {
                return 0;
            }

            // Calculate the movement speed
            return ApplyMovementSpeedMultipliers(RunSpeed);
        }

        public override void SetRotation(float rotation) {
            _rotate = 0; // not supported by this motor
        }

        public override bool IsMovingBackwards() {
            return false; // not supported by this motor
        }

        public override bool AllowsCameraAlignment() {
            // Only allow alignment when locked on target
            return IsLockedOnTarget();
        }

        protected override bool IsStrafing() {
            return base.IsStrafing() 
                    || (StrafeWhenLockedOnTarget && IsLockedOnTarget());
        }
    }
}