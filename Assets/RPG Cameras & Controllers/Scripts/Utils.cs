using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JohnStairs.RCC {
    public static class Utils {
        /// <summary>
        /// Warning message if a input was not found when trying to retrieve it
        /// </summary>
        private const string InputNotDefinedWarning = "An input which is used by one or more scripts is not defined: ";

        /// <summary>
        /// Enum for the different phases an input can have in Unity. Refer https://docs.unity3d.com/ScriptReference/Input.html for more information
        /// </summary>
        public enum InputPhase {
            Pressed,
            Down,
            Up,
            Smoothed,
            Raw
        }

        /// <summary>
		/// Tries to get the button input of the given input name and logs warnings if they could not be found
		/// </summary>
		/// <param name="phase">Input phase to check for</param>
		/// <param name="inputString">Name of the input to retrieve</param>
		/// <param name="logWarning">If true, warnings are logged if the input could not be found</param>
		/// <returns>True if input was found and given phase triggered, otherwise false</returns>
        public static bool TryGetButton(InputPhase phase, string inputString, bool logWarning = false) {
            bool input = false;

            try {
                if (phase == InputPhase.Pressed) {
                    input = Input.GetButton(inputString);
                } else if (phase == InputPhase.Down) {
                    input = Input.GetButtonDown(inputString);
                } else if (phase == InputPhase.Up) {
                    input = Input.GetButtonUp(inputString);
                }
            } catch (Exception) {
                if (logWarning) {
                    Debug.LogWarning(InputNotDefinedWarning + inputString);
                }
            }

            return input;
        }

        /// <summary>
        /// Tries to get the input axis of the given input name and logs warnings if they could not be found
        /// </summary>
        /// <param name="phase">Input phase to check for</param>
        /// <param name="inputString">Name of the input to retrieve</param>
        /// <param name="logWarning">If true, warnings are logged if the input could not be found</param>
        /// <returns>Input value if input was found, otherwise 0</returns>
        public static float TryGetAxis(InputPhase phase, string inputString, bool logWarning = false) {
            float input = 0;

            try {
                if (phase == InputPhase.Smoothed) {
                    input = Input.GetAxis(inputString);
                } else if (phase == InputPhase.Raw) {
                    input = Input.GetAxisRaw(inputString);
                }
            } catch (Exception) {
                if (logWarning) {
                    Debug.LogWarning(InputNotDefinedWarning + inputString);
                }
            }

            return input;
        }

        /// <summary>
        /// A custom modulo operation for calculating mod of a negative number as well
        /// </summary>
        /// <param name="dividend">Dividend</param>
        /// <param name="divisor">Divisor</param>
        /// <returns>The modulo of the given dividend divided by the given divisor</returns>
        public static float CustomModulo(float dividend, float divisor) {
            if (dividend < 0) {
                return dividend - divisor * Mathf.Ceil(dividend / divisor);
            } else {
                return dividend - divisor * Mathf.Floor(dividend / divisor);
            }
        }

        /// <summary>
        /// Checks if two floats are considered equal according to the given epsilon. Returns true if the distance between a and b is smaller than epsilon
        /// </summary>
        /// <param name="a">Left-side float</param>
        /// <param name="b">Right-side float</param>
        /// <param name="epsilon">Minimum value for inequality</param>
        /// <returns>True if the distance between a and b is smaller than epsilon</returns>
        public static bool IsAlmostEqual(float a, float b, float epsilon) {
            return Mathf.Abs(a - b) < epsilon;
        }

        /// <summary>
        /// Checks if the given vectors are almost equal, i.e. their angle is smaller than 1 degree
        /// </summary>
        /// <param name="a">Left-side vector</param>
        /// <param name="b">Right-side vector</param>
        /// <param name="epsilon">Minimum value for inequality</param>
        /// <returns>True if the given vectors are almost equal</returns>
        public static bool IsAlmostEqual(Vector3 a, Vector3 b, float epsilon) {
            return Vector3.Angle(a, b) < epsilon;
        }

        /// <summary>
        /// Returns the signed angle between vector a and b on the plane with normal normal. The result range is in (-180, 180]
        /// </summary>
        /// <param name="a">First vector</param>
        /// <param name="b">Second vector</param>
        /// <param name="normal">Plane normal for projecting vector a and b</param>
        /// <returns>Signed angle on plane with normal normal</returns>
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 normal) {
            // Project a and b onto the plane with normal normal
            a = Vector3.ProjectOnPlane(a, normal);
            b = Vector3.ProjectOnPlane(b, normal);
            // Calculate the signed angle between them
            return Vector3.SignedAngle(a, b, normal);
        }

        /// <summary>
        /// Projects the given vector onto the XZ plane and normalizes it
        /// </summary>
        /// <param name="vector">The vector to project</param>
        /// <returns>Vector vector projected onto the XZ plane</returns>
        public static Vector3 ProjectOnHorizontalPlane(Vector3 vector) {
            vector.y = 0;
            return Vector3.Normalize(vector);
        }

        /// <summary>
        /// Checks if the given layer is part of the given layer mask
        /// </summary>
        /// <param name="layer">Layer to check</param>
        /// <param name="layerMask">Layer mask to look in for layer</param>
        /// <returns>True if layer is in layerMask, otherwise false</returns>
        public static bool LayerInLayerMask(int layer, LayerMask layerMask) {
            return (layerMask.value & (1 << layer)) > 0;
        }

        /// <summary>
        /// Returns the normalized value of the given input, i.e. 1, -1 or 0
        /// </summary>
        /// <param name="input">Input to be normalized</param>
        /// <returns>Normalized input, i.e. either 1, -1 or 0</returns>
        public static float Normalize(float input) {
            if (IsAlmostEqual(input, 0, 0.05f)) {
                return 0;
            } else if (input > 0) {
                return 1.0f;
            } else {
                return -1.0f;
            }
        }

        /// <summary>
        /// Enables the ZWrite property of each shader assigned to materials of renderer r
        /// </summary>
        /// <param name="r">Renderer whose material shader ZWrite property should change</param>
        public static void EnableZWrite(Renderer r) {
            foreach (Material m in r.materials) {
                if (m.HasProperty("_Color")) {
                    m.SetInt("_ZWrite", 1);
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
        }

        /// <summary>
        /// Disables the ZWrite property of each shader assigned to materials of renderer r
        /// </summary>
        /// <param name="r">Renderer whose material shader ZWrite property should change</param>
        public static void DisableZWrite(Renderer r) {
            foreach (Material m in r.materials) {
                if (m.HasProperty("_Color")) {
                    m.SetInt("_ZWrite", 0);
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 100;
                }
            }
        }

        /// <summary>
		/// Checks if the pointer is over a GUI element
		/// </summary>
		/// <returns>True if the pointer is over a GUI element, otherwise false</returns>
        public static bool IsPointerOverGUI() {
            if (!EventSystem.current) {
                return false;
            }
            PointerEventData eventData = new PointerEventData(EventSystem.current) {
                position = Input.mousePosition
            };
            List<RaycastResult> raycastResults = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, raycastResults);
            return raycastResults.Count > 0;
        }

        /// <summary>
        /// Generate a random integer in the interval [min, max]
        /// </summary>
        /// <param name="min">Inclusive lower bound</param>
        /// <param name="max">Inclusive upper bound</param>
        /// <returns>A random integer from the interval [min, max]</returns>
        public static int RandomInteger(int min, int max) {
            return new System.Random().Next(min, max + 1);
        }

        /// <summary>
        /// Clamps the given vector to a maximum length of 1
        /// </summary>
        /// <param name="vector">Vector to be clamped in length</param>
        /// <returns>A vector of length 1 if the given vector had a length greater or equal 1. Otherwise, the given vector is just returned</returns>
        public static Vector3 ClampMagnitudeTo1(Vector3 vector) {
            if (vector.magnitude > 1) {
                return Vector3.Normalize(vector);
            }
            return vector;
        }
    }
}
