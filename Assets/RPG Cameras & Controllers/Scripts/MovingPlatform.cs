using System.Collections;
using System.Collections.Generic;
using JohnStairs.RCC.Character;
using UnityEngine;

namespace JohnStairs.RCC {
    public class MovingPlatform : MonoBehaviour {
        /// <summary>
        /// Start position
        /// </summary>
        [Tooltip("Start position.")]
        public Transform StartingPoint;
        /// <summary>
        /// Target position
        /// </summary>
        [Tooltip("Target position.")]
        public Transform TurningPoint;
        /// <summary>
        /// Time it takes for moving one way
        /// </summary>
        [Tooltip("Time it takes for moving one way.")]
        public float Duration = 2.0f;
        /// <summary>
        /// Time the platform waits before moving again
        /// </summary>
        [Tooltip("Time the platform waits before moving again.")]
        public float WaitTime = 2.0f;

        /// <summary>
        /// Start position
        /// </summary>
        protected Transform _start;
        /// <summary>
        /// Current target position to move to and align with
        /// </summary>
        protected Transform _target;
        /// <summary>
        /// Parameter for interpolation, here: passed time
        /// </summary>
        protected float _t;
        /// <summary>
        /// If true, the platform waits before moving on
        /// </summary>
        protected bool _waiting;
        /// <summary>
        /// The collider for detecting new or leaving passengers
        /// </summary>
        protected BoxCollider _triggerCollider;
        /// <summary>
        /// Set of all passengers with "GroundAffectsJumping" enabled
        /// </summary>
        protected HashSet<ITransportable> _specialPassengers;

        protected virtual void Start() {
            _start = StartingPoint;
            _target = TurningPoint;

            BoxCollider[] boxColliders = GetComponents<BoxCollider>();
            foreach (BoxCollider boxCollider in boxColliders) {
                if (boxCollider.isTrigger) {
                    _triggerCollider = boxCollider;
                    break;
                }
            }

            if (!_triggerCollider) {
                Debug.LogWarning("No trigger collider on game object " + name + " found! Please attach a collider with \"Is Trigger\" = true to make the MovingPlatform component work");
            }

            _specialPassengers = new HashSet<ITransportable>();
        }

        // FixedUpdate because of character controller collision detection
        protected virtual void FixedUpdate() {
            if (Vector3.Distance(transform.position, _target.position) < 0.01f
                && Quaternion.Angle(transform.rotation, _target.rotation) < 0.5f
                && !_waiting) {
                if (_target == TurningPoint) {
                    // Turning point reached
                    _start = TurningPoint;
                    _target = StartingPoint;
                } else {
                    // Back at starting point
                    _start = StartingPoint;
                    _target = TurningPoint;
                }
                // Reset the interpolation parameter
                _t = 0;
                // Pause movement
                StartCoroutine(WaitingCoroutine());
            }

            HashSet<ITransportable> currentPassengers = new HashSet<ITransportable>();
            // Process the special passengers to check if they are still aboard
            foreach (ITransportable passenger in _specialPassengers) {
                if (IsAbovePlatform(passenger)) {
                    currentPassengers.Add(passenger);
                } else {
                    // No longer above the platform => no longer consider it as passenger and unparent it
                    UnparentMotor(passenger);
                }
            }
            _specialPassengers = currentPassengers;

            if (_waiting) {
                // Continue waiting
                return;
            }

            _t += Time.deltaTime / Duration;

            Vector3 deltaTranslation = transform.position;
            Quaternion deltaRotation = transform.rotation;
            // Translate and rotate this object
            transform.position = Vector3.Lerp(_start.position, _target.position, _t);
            transform.rotation = Quaternion.Lerp(_start.rotation, _target.rotation, _t);
            // Compute the deltas
            deltaTranslation = transform.position - deltaTranslation;
            deltaRotation = transform.rotation * Quaternion.Inverse(deltaRotation);

            foreach (Transform child in transform) {
                ITransportable passenger = child.GetComponent<ITransportable>();
                if (passenger != null) {
                    passenger.OnExternalMovement(deltaTranslation, deltaRotation);
                }
            }
        }

        /// <summary>
        /// Coroutine for waiting WaitTime seconds at the start/turning point
        /// </summary>
        protected virtual IEnumerator WaitingCoroutine() {
            _waiting = true;
            yield return new WaitForSeconds(WaitTime);
            _waiting = false;
        }

        /// <summary>
        /// Checks if a passenger is still above the platform. Usually used for checking passengers with "GroundAffectsJumping" to see if 
        /// they left the inertial space via jumping or not
        /// </summary>
        /// <param name="transportable">The ITransportable component of the passenger to check</param>
        /// <returns>True if the passenger is above this object's trigger collider, otherwise false</returns>
        protected virtual bool IsAbovePlatform(ITransportable transportable) {
            Vector3 triggerSize = _triggerCollider.size; // local trigger collider size
            Vector3 characterPositionLocal = transform.InverseTransformPoint(transportable.GetTransform().position);

            if (characterPositionLocal.y < _triggerCollider.center.y - triggerSize.y * 0.5f) {
                // Position is below the box collider => passenger left the platform
                return false;
            }

            // ITransportable has the Character Controller as a prerequisite => get the radius of its collider
            float characterControllerRadius = transportable.GetColliderRadius();

            if (characterPositionLocal.x - characterControllerRadius <= triggerSize.x * 0.5f
                && characterPositionLocal.x + characterControllerRadius >= -triggerSize.x * 0.5f
                && characterPositionLocal.z - characterControllerRadius <= triggerSize.z * 0.5f
                && characterPositionLocal.z + characterControllerRadius >= -triggerSize.z * 0.5f) {
                // Passenger is between the bounds of the trigger collider in the XZ plane
                return true;
            }

            // Passenger must have left the platform
            return false;
        }

        /// <summary>
        /// Makes the passed ITransportable a child of this moving platform game object
        /// </summary>
        /// <param name="transportable">To-be child of this object</param>
        protected virtual void ParentMotor(ITransportable transportable) {
            transportable.GetTransform().SetParent(this.transform);
        }

        /// <summary>
        /// Removes the passed child ITransportable this moving platform game object
        /// </summary>
        /// <param name="transportable">Child ITransportable to be unparented</param>
        protected virtual void UnparentMotor(ITransportable transportable) {
            if (transportable.GetTransform().parent == this.transform) {
                transportable.GetTransform().SetParent(null);
            }
        }

        /// <summary>
        /// "OnTriggerEnter happens on the FixedUpdate function when two GameObjects collide" - Unity Documentation
        /// </summary>
        /// <param name="other">Collider that entered the trigger collider</param>
        protected virtual void OnTriggerEnter(Collider other) {
            ITransportable transportable = other.GetComponent<ITransportable>();

            if (transportable != null && transportable.IsMovingWithMovingGround()) {
                ParentMotor(transportable);

                if (transportable.IsGroundAffectingJumping()) {
                    // Memorize them as we cannot unparent them in OnTriggerExit
                    _specialPassengers.Add(transportable);
                }
            }
        }

        /// <summary>
        /// "OnTriggerExit is called when the Collider other has stopped touching the trigger" - Unity Documentation
        /// </summary>
        /// <param name="other">Left trigger collider</param>
        protected virtual void OnTriggerExit(Collider other) {
            ITransportable transportable = other.GetComponent<ITransportable>();

            if (transportable != null
                && transportable.IsMovingWithMovingGround()
                && !transportable.IsGroundAffectingJumping()) {
                // Motors with "GroundAffectsJumping" enabled are handled separately
                UnparentMotor(transportable);
            }
        }

        /// <summary>
        /// If Gizmos are enabled, this method draws some utility/debugging spheres
        /// </summary>
        protected virtual void OnDrawGizmos() {
            Gizmos.color = Color.gray;

            if (StartingPoint) {
                Gizmos.DrawWireCube(StartingPoint.position, transform.localScale);
            }

            if (TurningPoint) {
                Gizmos.DrawWireCube(TurningPoint.position, transform.localScale);
            }
        }
    }
}
