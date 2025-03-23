using UnityEngine;

namespace JohnStairs.RCC.Character {
	public interface ITransportable {
		/// <summary>
		/// Gets the transform of the transportable object
		/// </summary>
		/// <returns>Unity transform component</returns>
		Transform GetTransform();

		/// <summary>
		/// Gets the collider radius
		/// </summary>
		/// <returns>Collider radius in world space</returns>
		float GetColliderRadius();
		
		/// <summary>
		/// Checks if the object is moving with its ground
		/// </summary>
		/// <returns>True if its movement is influenced</returns>
		bool IsMovingWithMovingGround();
		
		/// <summary>
		/// Checks if the object's jumping ability is influenced by its ground
		/// </summary>
		/// <returns>True if its jumping is influenced</returns>
		bool IsGroundAffectingJumping();
		
		/// <summary>
		/// Processes external movement
		/// </summary>
		/// <param name="translation">Happened translation</param>
		/// <param name="rotation">Happened rotation</param>
		void OnExternalMovement(Vector3 translation, Quaternion rotation);
	}
}
