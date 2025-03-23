using JohnStairs.RCC.Character.Cam.Enums;
using JohnStairs.RCC.Enums;
using UnityEngine;

namespace JohnStairs.RCC.Character.Cam {
    public interface IRPGViewFrustum {
        /// <summary>
        /// Checks if the given camera is touching the ground
        /// </summary>
        /// <returns>True if it is touching the ground</returns>
		bool IsTouchingGround();

        /// <summary>
        /// Gets the shape of the view frustum
        /// </summary>
        /// <returns>Shape of the frustum</returns>
        FrustumShape GetShape();

        /// <summary>
        /// Gets the occluding layers that are processed by the view frustum
        /// </summary>
        /// <returns>Layer mask with occluding layers</returns>
        LayerMask GetOccludingLayers();

        /// <summary>
        /// Checks for objects inside the view frustum and - depending on the handling - fades them out or lets the camera zoom in. 
        /// Returns -1 if there is no ambient occlusion, otherwise returns the closest possible distance so that the sight to the target is clear
        /// </summary>
        /// <param name="from">Beginning of the view frustum</param>
        /// <param name="to">End of the view frustum</param>
        /// <returns>The closest possible distance so that the sight to the target is clear</returns>
        float CheckForOcclusion(Vector3 from, Vector3 to);

        /// <summary>
        /// Handles the fading of qualified objects between from and to
        /// </summary>
        /// <param name="from">Beginning of the area to check</param>
        /// <param name="to">End of the area to check</param>
        void HandleObjectFading(Vector3 from, Vector3 to);

        /// <summary>
        /// Draws the view frustum via debugging lines
        /// </summary>
        /// <param name="from">Beginning of the frustum</param>
        /// <param name="to">End of the frustum</param>
        /// <param name="withCameraPlane">If true, the camera plane at the cameraToUse's position is drawn additionally</param>
        void DrawFrustum(Vector3 from, Vector3 to, bool withCameraPlane = false);
    }
}
