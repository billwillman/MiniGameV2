namespace JohnStairs.RCC.Character.Motor.Enums {
    /// <summary>
    /// Enum for controlling when movement in midair is allowed
    /// </summary>
    public enum MidairMovement {
        /// <summary>
        /// Never allow midair movement
        /// </summary>
        Never,
        /// <summary>
        /// Only allow midair movement after performing a standing jump
        /// </summary>
        OnlyAfterStandingJump,
        /// <summary>
        /// Always allow midair movement
        /// </summary>
        Always
    }
}
