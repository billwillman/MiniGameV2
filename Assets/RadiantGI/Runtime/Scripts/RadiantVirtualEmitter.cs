using UnityEngine;
using System.Collections.Generic;

namespace RadiantGI.Universal {

    [ExecuteInEditMode]
    public class RadiantVirtualEmitter : MonoBehaviour {

        public enum EmitterShape {
            Point = 0,
            Box = 1
        }

        [Header("GI Color")]
        [ColorUsage(showAlpha: false, hdr: true)]
        public Color color = new Color(1, 1, 1);
        [Tooltip("Enable this option to add the emission color of the material used by this object to the global illumination.")]
        public bool addMaterialEmission;
        [Tooltip("The renderer from which synchronize the emission color")]
        public Renderer targetRenderer;
        [Tooltip("Optionally specify the material for the emission color")]
        public Material material;
        public string emissionPropertyName = "_EmissionColor";
        [Tooltip("Useful in case the gameobject uses more than one material")]
        public int materialIndex;
        public float intensity = 1f;
        [Tooltip("Falloff distance from the point emitter center or from the closest point on a box emitter.")]
        public float range = 10f;
        public EmitterShape shape = EmitterShape.Point;
        [Tooltip("World-space size used by box emitters. The box is centered on this transform position.")]
        public Vector3 shapeSize = Vector3.one;

        [Header("Area Of Influence")]
        public Vector3 boxCenter;
        public Vector3 boxSize = new Vector3(25, 25, 25);
        public bool boundsInLocalSpace = true;
        public float fadeDistance;

        [Tooltip("Surfaces in these rendering layers will receive lighting from this emitter. Only used when 'Virtual Emitters - Use Rendering Layers' is enabled in the Radiant Volume profile.")]
        public int renderingLayerMask = -1;   // -1 = Everything; bit pattern matches URP rendering layers (32-bit)

        int emissionNameId;
        Renderer thisRenderer;

        static List<Material> sharedMaterials = new List<Material>();

        private void OnValidate() {
            intensity = Mathf.Max(0, intensity);
            range = Mathf.Max(0, range);
            shapeSize.x = Mathf.Max(0, shapeSize.x);
            shapeSize.y = Mathf.Max(0, shapeSize.y);
            shapeSize.z = Mathf.Max(0, shapeSize.z);
            fadeDistance = Mathf.Max(0, fadeDistance);
        }

        void OnEnable() {
            emissionNameId = Shader.PropertyToID(emissionPropertyName);
            thisRenderer = GetComponentInChildren<Renderer>();
            RadiantRenderFeature.RegisterVirtualEmitter(this);
        }

        void OnDisable() {
            RadiantRenderFeature.UnregisterVirtualEmitter(this);
        }


        public Color GetGIColor() {
            Color sum = color;
            if (addMaterialEmission) {
                Material mat = material;
                if (mat == null) {
                    Renderer r = targetRenderer != null ? targetRenderer : thisRenderer;
                    if (r != null) {
                        if (materialIndex == 0) {
                            mat = r.sharedMaterial;
                        } else {
                            r.GetSharedMaterials(sharedMaterials);
                            if (materialIndex < sharedMaterials.Count) {
                                mat = sharedMaterials[materialIndex];
                            }
                        }
                    }
                }
                if (mat != null && mat.HasProperty(emissionNameId)) {
                    sum += mat.GetColor(emissionNameId);
                }
            }
            return sum * intensity;
        }


        public Vector4 GetGIColorAndRange() {
            Color giColor = GetGIColor();
            return new Vector4(giColor.r, giColor.g, giColor.b, range);
        }

        /// <summary>
        /// Returns emitter source shape in world space.
        /// </summary>
        /// <returns></returns>
        public Bounds GetShapeBounds() {
            return new Bounds(transform.position, shapeSize);
        }

        /// <summary>
        /// Returns the world-space AABB covered by the (possibly rotated) emitter shape plus its range.
        /// </summary>
        public Bounds GetRangeBounds() {
            if (shape == EmitterShape.Box) {
                Vector3 worldExtents = GetRotatedShapeWorldExtents();
                Vector3 size = worldExtents * 2f + Vector3.one * range * 2f;
                return new Bounds(transform.position, size);
            }
            Vector3 sphereSize = Vector3.one * range * 2f;
            return new Bounds(transform.position, sphereSize);
        }

        /// <summary>
        /// Returns the half-extents of the world-space AABB enclosing the box shape, accounting for transform rotation.
        /// </summary>
        public Vector3 GetRotatedShapeWorldExtents() {
            Vector3 localExt = shapeSize * 0.5f;
            Quaternion rot = transform.rotation;
            // Sum of |R[i]| * extent[i] for each world axis = AABB enclosing the rotated OBB.
            Vector3 right   = rot * Vector3.right;
            Vector3 up      = rot * Vector3.up;
            Vector3 forward = rot * Vector3.forward;
            return new Vector3(
                Mathf.Abs(right.x)   * localExt.x + Mathf.Abs(up.x) * localExt.y + Mathf.Abs(forward.x) * localExt.z,
                Mathf.Abs(right.y)   * localExt.x + Mathf.Abs(up.y) * localExt.y + Mathf.Abs(forward.y) * localExt.z,
                Mathf.Abs(right.z)   * localExt.x + Mathf.Abs(up.z) * localExt.y + Mathf.Abs(forward.z) * localExt.z
            );
        }

        /// <summary>
        /// Returns emitter area of influence in world space
        /// </summary>
        /// <returns></returns>
        public Bounds GetBounds() {
            Bounds bounds = new Bounds(boxCenter, boxSize);
            if (boundsInLocalSpace) {
                bounds.center += transform.position;
            }
            return bounds;
        }


        /// <summary>
        /// Sets emitter area of influence in world space
        /// </summary>
        /// <param name="bounds"></param>
        public void SetBounds(Bounds bounds) {
            if (boundsInLocalSpace) {
                bounds.center -= transform.position;
            }
            boxCenter = bounds.center;
            boxSize = bounds.size;
        }

    }

}
