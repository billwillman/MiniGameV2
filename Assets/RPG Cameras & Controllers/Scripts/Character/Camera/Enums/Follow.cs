namespace JohnStairs.RCC.Character.Cam.Enums {
    /// <summary>
    /// Enum for setting how the camera should follow its pivot
    /// </summary>
    public enum Follow {
        /// <summary>
        /// Always follow the camera pivot
        /// </summary>
        Strict,
        /// <summary>
        /// Only move when the camera gets too close (MinDistance) or too far away (MaxDistance) from the character
        /// </summary>
        Lazy
    }
}
