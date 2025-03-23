using UnityEngine;

namespace JohnStairs.RCC.Inputs {
	public interface IPointerInfo {
		/// <summary>
		/// Checks if the pointer is over a GUI element
		/// </summary>
		/// <returns>True if the pointer is over a GUI element, otherwise false</returns>
		bool IsPointerOverGUI();
	}
}
