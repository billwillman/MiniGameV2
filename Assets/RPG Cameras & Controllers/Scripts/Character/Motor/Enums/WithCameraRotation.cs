namespace JohnStairs.RCC.Character.Motor.Enums {
    /// <summary>
    /// Enum for controlling if the camera should rotate together with the character
    /// </summary>
    public enum WithCameraRotation {
        /// <summary>
        /// Never rotate with the character
        /// </summary>
        Never,
        /// <summary>
        /// The rotation stops when the stopping input is pressed. The input can be set inside the RPGCamera
        /// </summary>
        PreventOnInput,
        /// <summary>
        /// Always rotate together with the character
        /// </summary>
        Always
    }
}
