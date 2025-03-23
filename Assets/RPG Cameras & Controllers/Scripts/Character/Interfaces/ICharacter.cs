using UnityEngine;

namespace JohnStairs.RCC.Character {
	public interface ICharacter {
		/// <summary>
		/// Checks if the character is able to move
		/// </summary>
		/// <returns>True if the character can move, otherwise false</returns>
		bool CanMove();
		
		/// <summary>
		/// Checks if the character is able to rotate
		/// </summary>
		/// <returns>True if the character can rotate, otherwise false</returns>
		bool CanRotate();

		/// <summary>
		/// Checks if the character is able to fly
		/// </summary>
		/// <returns>True if the character can fly, otherwise false</returns>
		bool CanFly();

		/// <summary>
		/// Checks if the character is able to sprint
		/// </summary>
		/// <returns>True if the character can sprint, otherwise false</returns>
		bool CanSprint();
		
		/// <summary>
		/// Gets the movement speed modifier based on movement impairing effects. A modifier of 1.0f implies no movement speed change
		/// </summary>
		/// <returns>Modifier value for multiplication with the current movement speed</returns>
		float GetMovementSpeedModifier();

		/// <summary>
		/// Gets the target's position in world coordinates
		/// </summary>
		/// <returns>Target position in world coordinates</returns>
		Vector3 GetTargetPosition();
	}
}
