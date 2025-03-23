namespace JohnStairs.RCC.Character.Cam.Enums {
    /// <summary>
    /// Enum for controlling which camera GameObject should be used by this script
    /// </summary>
    public enum CameraSelection {
        /// <summary>
        /// Use the camera which is tagged as "MainCamera"
        /// </summary>
        MainCamera,
        /// <summary>
        /// Spawn a new camera game object in the scene to use
        /// </summary>
        SpawnOwnCamera,
        /// <summary>
        /// Use the camera game object assigned to public variable "UsedCamera"
        /// </summary>
        AssignedCamera
    }
}
