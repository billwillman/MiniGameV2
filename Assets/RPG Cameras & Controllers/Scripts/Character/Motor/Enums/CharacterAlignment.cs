namespace JohnStairs.RCC.Character.Motor.Enums {
    /// <summary>
    /// Enum for controlling the alignment of the character with the camera view
    /// </summary>
    public enum CharacterAlignment {
        /// <summary>
        /// Never align the character with the camera
        /// </summary>
        Never,
        /// <summary>
        /// Only align when the alignment input set inside the RPGCamera scripts is pressed
        /// </summary>
        OnAlignmentInput,
        /// <summary>
        /// Always align the character with the camera
        /// </summary>
        Always
    }
}
