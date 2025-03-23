namespace JohnStairs.RCC.Character {
	public interface IPlayer : ICharacter {
		/// <summary>
		/// Checks if the character locked onto a target, e.g. for combat
		/// </summary>
		/// <returns>True if the character locked onto a target, otherwise false</returns>
		bool LockedOnTarget();
	}
}
