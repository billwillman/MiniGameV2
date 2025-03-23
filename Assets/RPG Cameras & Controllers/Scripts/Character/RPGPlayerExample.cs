using JohnStairs.RCC.Inputs;
using UnityEngine;

namespace JohnStairs.RCC.Character {
    public class RPGPlayerExample : MonoBehaviour, IPlayer, IPointerInfo {
        public bool EnableFlying;
        public bool EnableMovement = true;
        public bool EnableRotation = true;
        public bool AllowSprinting = true;
        public GameObject Target;
        public bool EnableTargetLock;

        protected virtual void Start() {
            Target = GameObject.Find("Target Capsule");
        }

        protected virtual void Update() {
            if (Input.GetKeyDown(KeyCode.L)) {
                EnableTargetLock = !EnableTargetLock;
            }
        }

        public virtual bool CanFly() {
            return EnableFlying;
        }

        public virtual bool CanMove() {
            return EnableMovement;
        }

        public virtual bool CanRotate() {
            return EnableRotation;
        }

        public virtual bool CanSprint() {
            return AllowSprinting;
        }

        public virtual float GetMovementSpeedModifier() {
            return 1.0f; // Just return the default value if there is no movement speed impairment (1.0f == 100%)
        }

        public virtual Vector3 GetTargetPosition() {
            return Target?.transform.position ?? Vector3.zero;
        }

        public virtual bool LockedOnTarget() {
            return EnableTargetLock;
        }

        public virtual bool IsPointerOverGUI() {
            return Utils.IsPointerOverGUI();
        }
    }
}

