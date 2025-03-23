namespace JohnStairs.RCC.Character.Motor {
    public interface IRPGController {
        /// <summary>
        /// Activates input processing in this controller, making the referenced motor controllable
        /// </summary>
        void ActivateControls();

        /// <summary>
        /// Deactivates input processing in this controller. As a result, every player input is ignored
        /// </summary>
        void DeactivateControls();
    }
}
