namespace JohnStairs.RCC.Character.Motor.Enums {
    /// <summary>
    /// Represents the current state of climbing
    /// </summary>
    public enum ClimbingState {
        /// <summary>
        /// Not climbing at all
        /// </summary>
        None,
        /// <summary>
        /// Free climbing, i.e. climbing a surface in all directions 
        /// </summary>
        FreeClimbing,
        /// <summary>
        /// Hanging onto a ledge (inclusive movement)
        /// </summary>
        GrabbingLedge,
        /// <summary>
        /// In the process of climbing up a ledge
        /// </summary>
        ClimbingUpLedge
    }
}
