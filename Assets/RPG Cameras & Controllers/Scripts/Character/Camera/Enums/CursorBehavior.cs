namespace JohnStairs.RCC.Character.Cam.Enums {
    /// <summary>
    /// Enum for controlling cursor behavior
    /// </summary>
    public enum CursorBehavior {
        /// <summary>
        /// Default, unrestricted cursor movement
        /// </summary>
        Move,
        /// <summary>
        /// Cursor movement is confied by the screen edges
        /// </summary>
        MoveConfined,
        /// <summary>
        /// Cursor position should be stored and reloaded so that it looks like the cursor stays in position
        /// </summary>
        Stay,
        /// <summary>
        /// The cursor is locked in the center of the screen
        /// </summary>
        LockInCenter
    }
}
