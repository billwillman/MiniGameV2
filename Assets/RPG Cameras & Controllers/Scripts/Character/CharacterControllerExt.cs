using UnityEngine;

namespace JohnStairs.RCC.Character {
    public static class CharacterControllerExt {
        /// <summary>
        /// Gets the radius of the character controller collider in world scale. Uses transform.lossyScale
        /// </summary>
        /// <param name="characterController">Character controller component</param>
        /// <returns>Global collider radius</returns>
        public static float GetActualRadius(this CharacterController characterController) {
            return characterController.radius * Mathf.Max(characterController.transform.lossyScale.x, characterController.transform.lossyScale.z);
        }

        /// <summary>
        /// Gets the height of the character controller collider in world scale. Uses transform.lossyScale
        /// </summary>
        /// <param name="characterController">Character controller component</param>
        /// <returns>Global collider height</returns>
        public static float GetActualHeight(this CharacterController characterController) {
            return characterController.height * characterController.transform.lossyScale.y;
        }
    }
}
