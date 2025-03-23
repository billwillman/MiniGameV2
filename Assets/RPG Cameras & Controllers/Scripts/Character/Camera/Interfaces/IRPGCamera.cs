using JohnStairs.RCC.Enums;
using UnityEngine;

namespace JohnStairs.RCC.Character.Cam {
    public interface IRPGCamera {
        /// <summary>
        /// Gets the camera game component which is used by this RPGCamera script
        /// </summary>
        /// <returns></returns>
        Camera GetUsedCamera();

        /// <summary>
        /// Gets the viewport extents (half width and height) of the camera including set up viewport margins
        /// </summary>
        /// <returns>The viewport extents including possible margins</returns>
        Vector2 GetViewportExtentsWithMargin();

        /// <summary>
        /// Rotates the camera along the given axis 
        /// </summary>
        /// <param name="axis">Axis to rotate along, i.e. horizontal (X axis) or vertical (Y axis)</param>
        /// <param name="degrees">Degrees to rotate around the axis</param>
        /// <param name="immediately">If true, the camera is teleported to the new position</param>
        void Rotate(Axis axis, float degrees, bool immediately);

        /// <summary>
        /// Checks if the character is currently rotating with the camera 
        /// </summary>
        /// <returns>True if the character is rotated by the camera</returns>
        bool IsOrbitingWithCharacterRotation();
    }
}
