using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    public interface IRPGMotor {
        /// <summary>
        /// Checks if movement in all three directions is possible, e.g. while swimming or flying
        /// </summary>
        /// <returns>True if movement in all directions is enabled, otherwise false</returns>
        bool Is3dMovementEnabled();

        /// <summary>
        /// Checks if there is movement in the XZ plane, i.e. horizontal movement
        /// </summary>
        /// <returns>True if the object moves horizontally, otherwise false</returns>
        bool HasPlanarMovement();

        /// <summary>
        /// Checks if the object is moving backwards, i.e. facing opposite of the movement direction
        /// </summary>
        /// <returns>True if the object is moving backwards, otherwise false</returns>
        bool IsMovingBackwards();

        /// <summary>
        /// Returns if the character is swimming this frame
        /// </summary>
        /// <returns>True if the character is swimming</returns>
        bool IsSwimming();

        /// <summary>
        /// Checks if the motor logic allows an automatic alignment of the camera with the object
        /// </summary>
        /// <returns>True if camera alignment should be enabled, otherwise it is disabled</returns>
        bool AllowsCameraAlignment();

        /// <summary>
        /// Turns the object towards the given position
        /// </summary>
        /// <param name="position">Position the object should eventually face</param>
        /// <returns>The new facing direction</returns>
        Vector3 TurnTowards(Vector3 position);

        /// <summary>
        /// Rotates the object around the Y axis, i.e. vertically 
        /// </summary>
        /// <param name="input">Input value for the rotation</param>
        /// <param name="speed">Rotation speed</param>
        /// <param name="forceCameraRotation">If true, the camera is forced to rotate synchronously</param>
        /// <returns>The new facing direction of the object</returns>
        Vector3 RotateVertically(float input, float speed, bool forceCameraRotation = false);
    }
}
