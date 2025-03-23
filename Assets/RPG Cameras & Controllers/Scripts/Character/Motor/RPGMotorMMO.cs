using JohnStairs.RCC.Character.Motor.Enums;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    public class RPGMotorMMO : RPGMotor {
        /// <summary>
        /// If set to true, character 3D movement, e.g. diving or surfacing, is only possible when the character should align/rotate with the camera.
        /// Otherwise, the character will always move into viewing direction irrespective of alignment input
        /// </summary>
        [Tooltip("If set to true, character 3D movement, e.g. diving or surfacing, is only possible when the character should align/rotate with the camera. Otherwise, the character will always move into viewing direction irrespective of alignment input.")]
        public bool CameraControlled3dMovement = true;

        /// <summary>
        /// True if the current diving/surfacing direction should be kept, i.e. the character continues on its path instead of returning to an upright position
        /// </summary>
        protected bool _keepDivingDirection;
        /// <summary>
        /// The character's up axis while it should keep its diving direction
        /// </summary>
        protected Vector3 _divingDirectionUp;

        protected override Vector3 GetMovementDirection() {
            Vector3 input = _inputDirection;
            Vector3 up = Vector3.zero;
            input.x = _strafe;

            if (IsStrafingViaRotationModifier()) {
                // Let the character strafe instead of rotating
                input.x += _rotate;
                input.x = Mathf.Clamp(input.x, -1.0f, 1.0f);
            }

            if (IsLockedOnTarget()) {
                Vector3 forward = Utils.ProjectOnHorizontalPlane(GetTargetPosition() - transform.position);
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                if (_surface) {
                    up = Vector3.up;
                } else if (_dive) {
                    up = Vector3.down;
                }
                return Utils.ClampMagnitudeTo1(right * input.x + forward * input.z + up);
            }

            if (_surface || _dive) {
                _keepDivingDirection = false;
                return Utils.ClampMagnitudeTo1(transform.right * input.x + GetForwardInHorizontalPlane() * input.z + (_surface ? Vector3.up : Vector3.down));
            }

            up = Vector3.up;
            if (_canRotate
                && _3dMovementEnabled
                && input != Vector3.zero) {
                if (UseCameraXRotation() && Allow3dMovement()) {
                    _keepDivingDirection = true;
                    up = _rpgCamera.GetUsedCamera().transform.up;
                    _divingDirectionUp = up;
                } else if (_keepDivingDirection) {
                    up = _divingDirectionUp;
                }
            } else {
                _keepDivingDirection = false;
            }

            return Utils.ClampMagnitudeTo1(transform.right * input.x + Vector3.Cross(transform.right, up) * input.z);
        }

        protected override Vector3 GetClimbingDirection() {
            Vector3 input = _inputDirection;
            // Let the character strafe instead of rotating
            input.x = _strafe + _rotate;
            input.x = Mathf.Clamp(input.x, -1.0f, 1.0f);

            // Transform the local movement direction to world space and clamp the magnitude
            return Utils.ClampMagnitudeTo1(transform.TransformDirection(input));
        }

        protected override Vector3 GetFacingDirection() {
            if (IsLockedOnTarget()) {
                return TurnTowards(GetTargetPosition());
            }

            if (_rotate != 0 && !_rotationInputModified) {
                RotateVertically(_rotate, RotationSpeed * Time.deltaTime);
            }

            Vector3 right = transform.right;
            if (UseCameraYRotation()) {
                // Horizontal rotation
                _alignmentInProgress = true;
                right = _rpgCamera.GetUsedCamera().transform.right;
            }

            Vector3 up = Vector3.up;
            if (_canMove
                && _3dMovementEnabled
                && HasPlanarMovement()
                && !IsCloseToWaterSurface()
                && !Utils.IsAlmostEqual(_movementDirection.y, 0, 0.01f)) { // prevent the cross product of two almost equal vectors
                // Vertical rotation
                up = Vector3.Cross(transform.right, IsMovingBackwards() ? _movementDirection : -_movementDirection);
            }

            return Vector3.Cross(right, up);
        }

        protected override float GetStandardMovementSpeed() {
            float resultingSpeed = RunSpeed;
            // Compute the speed combined of strafe and run speed
            if (_inputDirection.x != 0 || _inputDirection.z != 0) {
                resultingSpeed = (StrafeSpeed * Mathf.Abs(_inputDirection.x)
                                + RunSpeed * Mathf.Abs(_inputDirection.z))
                                / (Mathf.Abs(_inputDirection.x) + Mathf.Abs(_inputDirection.z));
            }

            resultingSpeed = ApplyMovementSpeedMultipliers(resultingSpeed);
            // Adjust the speed if moving backwards and not walking
            if (_inputDirection.z < 0 && !_walking) {
                resultingSpeed *= BackwardsSpeedMultiplier;
            }

            return resultingSpeed;
        }

        public override bool AllowsCameraAlignment() {
            // Always allow camera alignment
            return true;
        }

        /// <summary>
        /// Checks if it is potentially possible to move 3-dimensionally (e.g. swimming or flying)
        /// </summary>
        /// <returns>True if 3d movement is generally possible, otherwise false</returns>
        protected virtual bool Has3dMovementPotential() {
            return _3dMovementEnabled && HasPlanarMovement();
        }

        /// <summary>
        /// Checks if the character should align with the Y rotation of the camera
        /// </summary>
        /// <returns>True if alignment with the Y axis rotation should be done, otherwise false</returns>
        protected virtual bool UseCameraYRotation() {
            if (_rpgCamera?.GetUsedCamera()) {
                // Camera is defined
                return Has3dMovementPotential() && !CameraControlled3dMovement
                        || AlignWithCamera == CharacterAlignment.Always
                        || AlignWithCamera == CharacterAlignment.OnAlignmentInput && _alignWithCamera
                        || _alignmentInProgress;
            }
            return false;
        }

        /// <summary>
        /// Checks if the character should align with the X rotation of the camera
        /// </summary>
        /// <returns>True if alignment with the X axis rotation should be done, otherwise false</returns>
        protected virtual bool UseCameraXRotation() {
            if (_rpgCamera?.GetUsedCamera()) {
                // Camera is defined
                return !CameraControlled3dMovement
                        || _rpgCamera.IsOrbitingWithCharacterRotation()
                        || _alignmentInProgress;

            }
            return false;
        }
    }
}