using UnityEngine;
using System.Collections.Generic;

namespace JohnStairs.RCC {
    [RequireComponent(typeof(BoxCollider))]
    public class Water : MonoBehaviour {
        /// <summary>
        /// Water height/level in world coordinates, set in Awake
        /// </summary>
        protected float _globalWaterHeight = 0;

        /// <summary>
        /// Comparer class for comparing two waters
        /// </summary>
        public class WaterComparer : Comparer<Water> {
            /// <summary>
            /// Compares two waters by their water level
            /// </summary>
            /// <param name="a">Left-side water script</param>
            /// <param name="b">Right-side water script</param>
            /// <returns>A signed number indicating the relative values of a and b</returns>
            public override int Compare(Water a, Water b) {
                return a.GetHeight().CompareTo(b.GetHeight());
            }
        }

        protected virtual void Start() {
            // Set the global water height once
            BoxCollider collider = GetComponent<BoxCollider>();
            if (!collider.isTrigger) {
                Debug.LogWarning("Box Collider found on water game object " + name + " is not set up as trigger! However, this is needed for the swimming mechanic to work");
            }
            
            _globalWaterHeight = transform.position.y + (collider.center.y + collider.size.y * 0.5f) * transform.localScale.y;
            // Disable the ZWrite property
            DisableShaderZWrite();
        }

        /// <summary>
        /// Disables the ZWrite property of each material of this game object
        /// </summary>
        public virtual void DisableShaderZWrite() {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers) {
                Utils.DisableZWrite(r);
            }
        }

        /// <summary>
        /// Gets the water height/level in world coordinates
        /// </summary>
        /// <returns>Water level in world coordinates</returns>
        public virtual float GetHeight() {
            return _globalWaterHeight;
        }
    }
}
