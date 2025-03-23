using System.Collections;
using System.Collections.Generic;
using JohnStairs.RCC.Character.Cam;
using JohnStairs.RCC.Character.Motor.Enums;
using JohnStairs.RCC.Enums;
using UnityEngine;

namespace JohnStairs.RCC.Character.Motor {
    public abstract class RPGMotor : MonoBehaviour, IRPGMotor, ITransportable {
        /// <summary>
        /// Event which is fired once when the motor starts to move
        /// </summary>
        public event System.Action Motion;

        /// <summary>
        /// The speed of character movement when walking is toggled off. This is considered the default movement speed when no modifier is applied
        /// </summary>
        [Tooltip("The speed of character movement when walking is toggled off. This is considered the default movement speed when no modifier is applied.")]
        public float RunSpeed = 6.0f;
        /// <summary>
        /// The speed of character movement while solely strafing. A combined speed is computed when also running/walking forward/backwards
        /// </summary>
        [Tooltip("The speed of character movement while solely strafing. A combined speed is computed when also running/walking forward/backwards.")]
        public float StrafeSpeed = 6.0f;
        /// <summary>
        /// The speed of character movement when walking is toggled on
        /// </summary>
        [Tooltip("The speed of character movement when walking is toggled on.")]
        public float WalkSpeed = 2.0f;
        /// <summary>
        /// The speed of character movement when crouching is toggled on
        /// </summary>
        [Tooltip("The speed of character movement when crouching is toggled on.")]
        public float CrouchSpeed = 1.5f;
        /// <summary>
        /// The multiplier for computing the walking backwards speed
        /// </summary>
        [Tooltip("The multiplier for computing the walking backwards speed.")]
        public float BackwardsSpeedMultiplier = 0.5f;
        /// <summary>
        /// The multiplier for computing the sprinting speed while the sprint button is pressed
        /// </summary>
        [Tooltip("The multiplier for computing the sprinting speed while the sprint button is pressed.")]
        public float SprintSpeedMultiplier = 1.5f;
        /// <summary>
        /// The multiplier for computing the swim speed
        /// </summary>
        [Tooltip("The multiplier for computing the swim speed.")]
        public float SwimSpeedMultiplier = 1.0f;
        /// <summary>
        /// If set to true, the acceleration will be maintained and applied in m/s^2 (real acceleration). Otherwise, a duration for reaching the target movement speed, e.g. from standing still to running, will be maintained
        /// </summary>
        [Tooltip("If set to true, the acceleration will be maintained and applied in m/s^2 (real acceleration). Otherwise, a duration for reaching the target movement speed, e.g. from standing still to running, will be maintained.")]
        public bool SteadyAcceleration = false;
        /// <summary>
        /// Acceleration is measured in m/s^2, e.g. a value of 3 means that accelerating from 0 to 6 units/s would take 2 seconds
        /// </summary>
        [Tooltip("Acceleration is measured in m/s^2, e.g. a value of 3 means that accelerating from 0 to 6 units/s would take 2 seconds.")]
        public float Acceleration = 12;
        /// <summary>
        /// The time in seconds it takes to reach the target movement speed irrespective of the difference to the current speed
        /// </summary>
        [Tooltip("The time in seconds it takes to reach the target movement speed irrespective of the difference to the current speed.")]
        public float AccelerationTime = 0;
        /// <summary>
        /// Approximately the degrees per second the character needs for turning into a new direction
        /// </summary>
        [Tooltip("Approximately the degrees per second the character needs for turning into a new direction.")]
        public float RotationSpeed = 180.0f;
        /// <summary>
        /// The height the character jumps in world units
        /// </summary>
        [Tooltip("The height the character jumps in world units.")]
        public float JumpHeight = 1.5f;
        /// <summary>
        /// If set to true, jumps while the character is in midair are allowed. Used in combination with variable "AllowedMidairJumps"
        /// </summary>
        [Tooltip("If set to true, jumps while the character is in midair are allowed. Used in combination with variable \"Allowed Midair Jumps\".")]
        public bool EnableMidairJumps = false;
        /// <summary>
        /// The number of allowed jumps while the character is in midair/airborne, e.g. use value 1 for allowing double jumps
        /// </summary>
        [Tooltip("The number of allowed jumps while the character is in midair/airborne, e.g. use value 1 for allowing double jumps.")]
        public int AllowedMidairJumps = 1;
        /// <summary>
        /// If set to true, a midair jump, e.g. double jump, is rewarded if it is timed properly. The timing tolerance can be set up with variable PeakJumpHeightTolerance. Variable PerfectDoubleJumpMultiplier gives an example for an application by rewarding the player with additional jump height
        /// </summary>
        [Tooltip("If set to true, a midair jump, e.g. double jump, is rewarded if it is timed properly. The timing tolerance can be set up with variable PeakJumpHeightTolerance. Variable PerfectDoubleJumpMultiplier gives an example for an application by rewarding the player with additional jump height.")]
        public bool RewardPerfectMidairJump = true;
        /// <summary>
        /// Jump height multiplier when performing a perfect midair jump
        /// </summary>
        [Tooltip("Jump height multiplier when performing a perfect midair jump.")]
        public float PerfectDoubleJumpMultiplier = 1.5f;
        /// <summary>
        /// Tolerance for how long before and after the actual jump peak height a midair jump is considered as "perfect". The value is heavily depending on variables JumpHeight and Gravity. It is recommended to use the below debugging features to get a feeling for it
        /// </summary>
        [Tooltip("Tolerance for how long before and after the actual jump peak height a midair jump is considered as \"perfect\". The value is heavily depending on variables JumpHeight and Gravity. It is recommended to use the below debugging features to get a feeling for it.")]
        public float JumpPeakTolerance = 2.0f;
        /// <summary>
        /// If set to true, a sphere at the head of the character is drawn during jump peak
        /// </summary>
        [Tooltip("If set to true, a sphere at the head of the character is drawn during jump peak.")]
        public bool DrawGizmoDuringJumpPeak = false;
        /// <summary>
        /// If set to true, the duration of the jump peak height for a perfect midair jump is logged. The duration is depending on variable JumpPeakTolerance
        /// </summary>
        [Tooltip("If set to true, the duration of the jump peak height for a perfect midair jump is logged. The duration is depending on variable JumpPeakTolerance.")]
        public bool LogApproxJumpPeakDuration = false;
        /// <summary>
        /// Enum value for setting up when midair movement is allowed, e.g. never, only after a standing jump or always
        /// </summary>
        [Tooltip("Enum value for setting up when midair movement is allowed, e.g. never, only after a standing jump or always.")]
        public MidairMovement EnableMidairMovement = MidairMovement.OnlyAfterStandingJump;
        /// <summary>
        /// The speed of the movement impulses while in midair/airborne
        /// </summary>
        [Tooltip("The speed of the movement impulses while in midair/airborne.")]
        public float MidairSpeed = 2.0f;
        /// <summary>
        /// If set to true, the number of allowed moves while in midair/airborne is unlimited
        /// </summary>
        [Tooltip("If set to true, the number of allowed moves while in midair/airborne is unlimited.")]
        public bool UnlimitedMidairMoves = false;
        /// <summary>
        /// The number of allowed movement impulses while in midair/airborne
        /// </summary>
        [Tooltip("The number of allowed movement impulses while in midair/airborne.")]
        public int AllowedMidairMoves = 1;
        /// <summary>
        /// Local water height at which the character should start to swim. Enable Gizmos for easier setup
        /// </summary>
        [Tooltip("Local water height at which the character should start to swim. Enable Gizmos for easier setup.")]
        public float SwimmingStartHeight = 1.25f;
        /// <summary>
        /// If set to true, jumps while swimming at the water surface are possible
        /// </summary>
        [Tooltip("If set to true, jumps while swimming at the water surface are possible.")]
        public bool EnableSwimmingJumps = true;
        /// <summary>
        /// Enables/disables grabbing and climbing up ledges
        /// </summary>
        [Tooltip("Enables/disables grabbing and climbing up ledges.")]
        public bool EnableLedgeClimbing = true;
        /// <summary>
        /// The speed the character climbs up ledges
        /// </summary>
        [Tooltip("The speed the character climbs up ledges.")]
        public float LedgeClimbingUpSpeed = 3.0f;
        /// <summary>
        /// The height where the character should grab the ledge
        /// </summary>
        [Tooltip("The height where the character should grab the ledge.")]
        public float LedgeGrabHeight = 1.5f;
        /// <summary>
        /// The distance between the character and a potential ledge to grab
        /// </summary>
        [Tooltip("The distance between the character and a potential ledge to grab.")]
        public float LedgeGrabDistance = 0.1f;
        /// <summary>
        /// The maximum angle of a ledge which can be grabbed
        /// </summary>
        [Tooltip("The maximum angle of a ledge which can be grabbed.")]
        public float LedgeGrabMaxSlope = 40.0f;
        /// <summary>
        /// The time which has to pass before the next ledge grab is possible
        /// </summary>
        [Tooltip("The time which has to pass before the next ledge grab is possible.")]
        public float LedgeGrabCooldown = 0.5f;
        /// <summary>
        /// If there is no climbing progress within this time, the climb up is canceled
        /// </summary>
        [Tooltip("If there is no climbing progress within this time, the climb up is canceled.")]
        public float LedgeClimbingUpTimeout = 0.5f;
        /// <summary>
        /// If true, ledges are only grabbed when the character is falling
        /// </summary>
        [Tooltip("If true, ledges are only grabbed when the character is falling.")]
        public bool OnlyAllowLedgeGrabbingWhileFalling = false;
        /// <summary>
        /// If set to true, gizmos for the ledge grab and climb up checks are drawn
        /// </summary>
        [Tooltip("If set to true, gizmos for the ledge grab and climb up checks are drawn.")]
        public bool DrawLedgeCheckGizmos = true;
        /// <summary>
        /// Enables/disables free climbing colliders
        /// </summary>
        [Tooltip("Enables/disables free climbing colliders.")]
        public bool EnableFreeClimbing = true;
        /// <summary>
        /// The speed for climbing in all directions
        /// </summary>
        [Tooltip("The speed for climbing in all directions.")]
        public float ClimbingSpeed = 2.0f;
        /// <summary>
        /// Check height of the hands of the character
        /// </summary>
        [Tooltip("Check height of the hands of the character.")]
        public float FreeClimbingHandsHeight = 1.24f;
        /// <summary>
        /// Check height of the feet of the character
        /// </summary>
        [Tooltip("Check height of the feet of the character.")]
        public float FreeClimbingFeetHeight = 0.3f;
        /// <summary>
        /// The maximum corner angle the character can still climb around
        /// </summary>
        [Tooltip("The maximum corner angle the character can still climb around.")]
        public float FreeClimbingMaxCornerAngle = 60.0f;
        /// <summary>
        /// The time which has to pass to start free climbing again
        /// </summary>
        [Tooltip("The time which has to pass to start free climbing again.")]
        public float FreeClimbingCooldown = 0.5f;
        /// <summary>
        /// If true, free climbing can only be started grounded
        /// </summary>
        [Tooltip("If true, free climbing can only be started grounded.")]
        public bool OnlyAllowFreeClimbingWhileGrounded = false;
        /// <summary>
        /// If set to true, gizmos for the free climbing checks are drawn 
        /// </summary>
        [Tooltip("If set to true, gizmos for the free climbing checks are drawn .")]
        public bool DrawFreeClimbingCheckGizmos = true;
        /// <summary>
        /// The distance between the left and the right arm or leg, respectively, for free climbing
        /// </summary>
        [Tooltip("The distance between the left and the right arm or leg, respectively, for free climbing.")]
        public float GrabRange = 0.4f;
        /// <summary>
        /// The radius of the ledge and/or free climbing check (imagine it as the diameter of hands and feet)
        /// </summary>
        [Tooltip("The radius of the ledge and/or free climbing check (imagine it as the diameter of hands and feet).")]
        public float ClimbingCheckRadius = 0.1f;
        /// <summary>
        /// Layers which are checked for ledge grabbing and/or free climbing
        /// </summary>
        [Tooltip("Layers which are checked for ledge grabbing and/or free climbing.")]
        public LayerMask ClimbableLayers;
        /// <summary>
        /// Determines when the character's view/walking direction should be set to the camera's view direction. If set to "OnAlignmentInput", input "Align Character" is used to trigger the alignment
        /// </summary>
        [Tooltip("Determines when the character's view/walking direction should be set to the camera's view direction. If set to \"OnAlignmentInput\", input \"Align Character\" is used to trigger the alignment.")]
        public CharacterAlignment AlignWithCamera = CharacterAlignment.OnAlignmentInput;
        /// <summary>
        /// Determines if the camera should rotate in sync with character rotation. If set to "PreventOnInput", input "Pause Rotation With Character" is used for pausing rotation 
        /// </summary>
        [Tooltip("Determines if the camera should rotate in sync with character rotation. If set to \"PreventOnInput\", input \"Pause Rotation With Character\" is used for pausing rotation .")]
        public WithCameraRotation AlsoRotateCamera = WithCameraRotation.PreventOnInput;
        /// <summary>
        /// Approximately the time in seconds the character needs for turning into a new direction
        /// </summary>
        [Tooltip("Approximately the time in seconds the character needs for turning into a new direction.")]
        public float AlignmentTime = 0.1f;
        /// <summary>
        /// If set to true, the character passively moves with moving objects, e.g. moving platforms, when standing on them
        /// </summary>
        [Tooltip("If set to true, the character passively moves with moving objects, e.g. moving platforms, when standing on them.")]
        public bool MoveWithMovingGround = true;
        /// <summary>
        /// If set to true, the character’s jumping direction is affected by the ground object, i.e. performing a standing jump on a moving object lets the character always land on the same spot of the object
        /// </summary>
        [Tooltip("If set to true, the character’s jumping direction is affected by the ground object, i.e. performing a standing jump on a moving object lets the character always land on the same spot of the object.")]
        public bool GroundAffectsJumping = true;
        /// <summary>
        /// Layers which are ignored by motor physics logic, i.e. not considered for the grounded check, sliding and collision movement
        /// </summary>
        [Tooltip("Layers which are ignored by motor physics logic, i.e. not considered for the grounded check, sliding and collision movement.")]
        public LayerMask IgnoredLayers;
        /// <summary>
        /// If set to true, the character starts to slide on slopes steeper than the assigned CharacterController.slopeLimit
        /// </summary>
        [Tooltip("If set to true, the character starts to slide on slopes steeper than the assigned CharacterController.slopeLimit.")]
        public bool EnableSliding = true;
        /// <summary>
        /// Time in seconds which has to pass before sliding logic is applied
        /// </summary>
        [Tooltip("Time in seconds which has to pass before sliding logic is applied.")]
        public float SlidingTimeout = 0.1f;
        /// <summary>
        /// Sliding time in seconds which has to pass before the anti-stuck mechanic enables
        /// </summary>
        [Tooltip("Sliding time in seconds which has to pass before the anti-stuck mechanic enables.")]
        public float AntiStuckTimeout = 0.1f;
        /// <summary>
        /// If set to true, the standing character is pushed away by moving objects on collision
        /// </summary>
        [Tooltip("If set to true, the standing character is pushed away by moving objects on collision.")]
        public bool EnableCollisionMovement = true;
        /// <summary>
        /// Tolerance height used for the grounded check. The larger the value, the larger the distance to the ground which sets _grounded to true. Useful for tweaking movement on debris
        /// </summary>
        [Tooltip("Tolerance height used for the grounded check. The larger the value, the larger the distance to the ground which sets _grounded to true. Useful for tweaking movement on debris.")]
        public float GroundedTolerance = 0.16f;
        /// <summary>
        /// A value representing the degree at which the character starts to fall. The default value is 6 to let the character be grounded when walking down small hills
        /// </summary>
        [Tooltip("A value representing the degree at which the character starts to fall. The default value is 6 to let the character be grounded when walking down small hills.")]
        public float FallingThreshold = 6.0f;
        /// <summary>
        /// A value representing the downward force of gravity
        /// </summary>
        [Tooltip("A value representing the downward force of gravity.")]
        public float Gravity = 20.0f;
        /// <summary>
        /// Time in seconds which has to pass before the character can rotate up/down via camera rotation
        /// </summary>
        [Tooltip("Time in seconds which has to pass before the character can rotate up/down via camera rotation.")]
        public float FlyingTimeout = 0.2f;

        /// <summary>
        /// If true, the motor is completely initialized and can be started
        /// </summary>
        protected bool _initialized;
        /// <summary>
        /// The used character controller component
        /// </summary>
        protected CharacterController _characterController;
        /// <summary>
        /// The used animator component
        /// </summary>
        protected Animator _animator;
        /// <summary>
        /// Reference to a RPGCamera script
        /// </summary>
        protected IRPGCamera _rpgCamera;
        /// <summary>
        /// Interface for getting character information, e.g. movement impairing effects
        /// </summary>
        protected IPlayer _player;
        /// <summary>
        /// Direction given into the motor by the controller
        /// </summary>
        protected Vector3 _inputDirection;
        /// <summary>
        /// Movement direction in world coordinates
        /// </summary>
        protected Vector3 _movementDirection;
        /// <summary>
        /// Direction the character should eventually face
        /// </summary>
        protected Vector3 _facingDirection;
        /// <summary>
        /// Resulting movement speed of the character
        /// </summary>
        protected float _movementSpeed;
        /// <summary>
        /// True if the character is grounded
        /// </summary>
        protected bool _grounded = true;
        /// <summary>
        /// True if the character's X and Z rotation should be reset with the next fixed update
        /// </summary>
        protected bool _resetXandZRotations = false;
        /// <summary>
        /// True if the character should jump
        /// </summary>
        protected bool _jump = false;
        /// <summary>
        /// Horizontal rotation which should be performed in the next frame
        /// </summary>
        protected float _rotate = 0;
        /// <summary>
        /// True if horizontal rotation should be modified
        /// </summary>
        protected bool _rotationInputModified = false;
        /// <summary>
        /// Strafe amount that should be performed in the next frame
        /// </summary>
        protected float _strafe = 0;
        /// <summary>
        /// True if the character is automatically moving forward
        /// </summary>
        protected bool _autorunning = false;
        /// <summary>
        /// True if the character should crouch
        /// </summary>
        protected bool _crouching = false;
        /// <summary>
        /// True if the character should walk
        /// </summary>
        protected bool _walking = false;
        /// <summary>
        /// True if the character is swimming
        /// </summary>
        protected bool _swimming = false;
        /// <summary>
        /// True if the character is flying
        /// </summary>
        protected bool _flying = false;
        /// <summary>
        /// True if the character should surface
        /// </summary>
        protected bool _surface = false;
        /// <summary>
        /// True if the character should dive down
        /// </summary>
        protected bool _dive = false;
        /// <summary>
        /// True if the character is sprinting
        /// </summary>
        protected bool _sprinting = false;
        /// <summary>
        /// True if the character is sliding
        /// </summary>
        protected bool _sliding = false;
        /// <summary>
        /// Buffer for measuring how long consecutive sliding events occured
        /// </summary>
        protected float _slidingBuffer;
        /// <summary>
        /// True if the anti-stuck mechanic is enabled, i.e. the anti-stuck timeout occurred
        /// </summary>
        protected bool _antiStuckEnabled;
        /// <summary>
        /// True if the character is able to move
        /// </summary>
        protected bool _canMove = true;
        /// <summary>
        /// True if the character is able to move
        /// </summary>
        protected bool _canRotate = true;
        /// <summary>
        /// True if movement in all three directions should be possible, e.g. while swimming or flying
        /// </summary>
        protected bool _3dMovementEnabled;
        /// <summary>
        /// Buffer for measuring how long the character is flying
        /// </summary>
        protected float _flyingBuffer;
        /// <summary>
        /// Current movement speed acceleration velocity
        /// </summary>
        protected float _accelerationVelocity;
        /// <summary>
        /// Number of already performed jumps during the current midair period
        /// </summary>
        protected int _midairJumpsCount = 0;
        /// <summary>
        /// True if the logging of the jump peak duration is currently running
        /// </summary>
        protected bool _loggingJumpPeakDuration;
        /// <summary>
        /// True if the character performed a jump while running
        /// </summary>
        protected bool _runningJump = false;
        /// <summary>
        /// True if airborne movement is currently allowed
        /// </summary>
        protected bool _allowMidairMovement = false;
        /// <summary>
        /// Number of already performed moves during the current midair period
        /// </summary>
        protected int _midairMovesCount = 0;
        /// <summary>
        /// Height in world coordinates below which the character starts swimming
        /// </summary>
        protected float _swimmingStartHeight = -Mathf.Infinity;
        /// <summary>
        /// Stores all scripts of touched waters, sorted by water height/level
        /// </summary>
        protected SortedSet<Water> _touchedWaters;
        /// <summary>
        /// Water level on the world Y axis
        /// </summary>
        protected float _currentWaterHeight = -Mathf.Infinity;
        /// <summary>
        /// Turning direction of the character around the global Y axis
        /// </summary>
        protected float _turningDirection;
        /// <summary>
        /// Current rotation velocity for turning the character
        /// </summary>
        protected float _rotationVelocity;
        /// <summary>
        /// The climbing state in which the character is currently
        /// </summary>
        protected ClimbingState _climbingState;
        /// <summary>
        /// The sphere cast distance to check when the character is not already grabbing a ledge
        /// </summary>
        protected float _initialLedgeGrabCheckDistance = 0.2f;
        /// <summary>
        /// The height of the currently grabbed on ledge in world coordinates
        /// </summary>
        protected float _grabbedLedgeHeight;
        /// <summary>
        /// The time which passed while hanging/grabbing a ledge
        /// </summary>
        protected float _ledgeGrabTimer;
        /// <summary>
        /// Cooldown routine for ledge grabbing
        /// </summary>
        protected IEnumerator _ledgeGrabbingCooldown;
        /// <summary>
        /// True if ledge grabbing is on cooldown
        /// </summary>
        protected bool _ledgeGrabbingOnCooldown = false;
        /// <summary>
        /// Character position of the last frame during the climb up of a ledge
        /// </summary>
        protected Vector3 _lastLedgeClimbUpPosition;
        /// <summary>
        /// The time which the character did not move during the climb up of a ledge
        /// </summary>
        protected float _ledgeClimbUpStuckTimer;
        /// <summary>
        /// Alignment offset to the cliff which is climbed. Needed for adjusting the grab distance for ledge grabbing
        /// </summary>        
        protected Vector3 _cliffAlignmentOffset;
        /// <summary>
        /// True if free climbing is on cooldown
        /// </summary>
        protected bool _freeClimbingOnCooldown = false;
        /// <summary>
        /// True if the character should align with the camera view direction
        /// </summary>
        protected bool _alignWithCamera = false;
        /// <summary>
        /// True if the character is currently aligning
        /// </summary>
        protected bool _alignmentInProgress;
        /// <summary>
        /// True if the camera should not rotate in sync with character rotation
        /// </summary>
        protected bool _pauseCameraRotation = false;
        /// <summary>
        /// Bottom sphere of the character controller capsule collider in world space
        /// </summary>
        protected Sphere _colliderBottom;
        /// <summary>
        /// Stores the offset from the character position to the bottom of the character controller collider
        /// </summary>
        protected float _colliderBottomOffset;
        /// <summary>
        /// Current state the motor is in
        /// </summary>   
        protected MotorState _currentState;
        /// <summary>
        /// True if the Motion event was already invoked, otherwise false
        /// </summary>
        protected bool _motionEventInvoked;
        #region Animation parameter IDs
        protected static readonly int __localMovementX = Animator.StringToHash("Local Movement X");
        protected static readonly int __localMovementZ = Animator.StringToHash("Local Movement Z");
        protected static readonly int __movementSpeed = Animator.StringToHash("Movement Speed");
        protected static readonly int __turningDirection = Animator.StringToHash("Turning Direction");
        protected static readonly int __grounded = Animator.StringToHash("Grounded");
        protected static readonly int __crouching = Animator.StringToHash("Crouching");
        protected static readonly int __falling = Animator.StringToHash("Falling");
        protected static readonly int __sliding = Animator.StringToHash("Sliding");
        protected static readonly int __swimming = Animator.StringToHash("Swimming");
        protected static readonly int __flying = Animator.StringToHash("Flying");
        protected static readonly int __climbingState = Animator.StringToHash("Climbing State");
        protected static readonly int __jump = Animator.StringToHash("Jump");
        #endregion

        /// <summary>
        /// Helper structure, e.g. for representing the bottom of the character controller capsule collider
        /// </summary>
        protected class Sphere {
            /// <summary>
            /// Center of the sphere
            /// </summary>
            public Vector3 Center;
            /// <summary>
            /// Radius of the sphere
            /// </summary>
            public float Radius;
        }

        protected virtual void Awake() {
            _characterController = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();
            _rpgCamera = GetComponent<IRPGCamera>();
            _player = GetComponent<IPlayer>();
            _touchedWaters = new SortedSet<Water>(new Water.WaterComparer());
        }

        protected virtual void Start() {
            #region Inconsistency checks
            if (!Utils.LayerInLayerMask(gameObject.layer, IgnoredLayers)) {
                Debug.LogWarning("RPGMotor variable \"Ignored Layers\" does not contain the character's layer! Negative side effects might occur (see Manual section 1.2.3)");

                int playerLayerMask = LayerMask.NameToLayer("Player");
                if (IgnoredLayers == 0 && playerLayerMask != -1) {
                    IgnoredLayers = 1 << playerLayerMask;
                    Debug.LogWarning("RPGMotor variable \"Ignored Layers\" was \"Nothing\". Defaulted the layer mask to layer \"Player\"");
                }
            }

            int climbableLayerMask = LayerMask.NameToLayer("Climbable");
            if (ClimbableLayers == 0 && climbableLayerMask != -1) {
                ClimbableLayers = 1 << climbableLayerMask;
                Debug.LogWarning("RPGMotor variable \"Climbable Layers\" was \"Nothing\". Defaulted the layer mask to layer \"Climbable\"");
            }

            if (EnableFreeClimbing && EnableLedgeClimbing
                && FreeClimbingHandsHeight + ClimbingCheckRadius > LedgeGrabHeight + _initialLedgeGrabCheckDistance) {
                Debug.LogWarning("Free Climbing and Ledge Climbing are both enabled, but the used Hands Height for free climbing is too high to make ledge grabbing possible. Lower the Hands Height or increase the Ledge Grab Height to allow ledge grabbing while free climbing");
            }
            #endregion

            InitColliderBottom();
            // Compute the collider bottom offset once
            _colliderBottomOffset = (_colliderBottom.Center + Vector3.down * _colliderBottom.Radius).y - transform.position.y;

            _movementDirection.y = -1.0f; // to trigger the grounded check in the first frame already

            _facingDirection = transform.forward;

            _initialized = true;
        }

        protected virtual void FixedUpdate() {
            if (_resetXandZRotations
                && _grounded
                && !_swimming
                && !IsClimbing()) {
                _resetXandZRotations = false;
                // Ensure that the character's X and Z axes rotations are reset after swimming (and flying) to prevent tilted ground movement
                ResetXandZrotations();
            }

            if (EnableCollisionMovement && !IsClimbing()) {
                ApplyCollisionMovement();
            }
        }

        /// <summary>
        /// Starts motor computations based on external input
        /// </summary>
        public virtual void StartMotor() {
            if (!_initialized) {
                return;
            }

            _canMove = _player?.CanMove() ?? true;
            _canRotate = _player?.CanRotate() ?? true;

            _grounded = GroundedCheck();

            _swimming = SwimmingCheck();

            if (_swimming || !_grounded) {
                _resetXandZRotations = true;
            }

            _flying = IsFlying();

            if (_flying) {
                _flyingBuffer = Mathf.Max(-1.0f, _flyingBuffer - Time.deltaTime);
            } else {
                _flyingBuffer = FlyingTimeout;
            }

            _3dMovementEnabled = _swimming || _flying;
            if (_3dMovementEnabled) {
                _crouching = false;
            }

            if (_animator.GetBool(__jump)) {
                // Jump trigger was not consumed => reset it to prevent delayed jumping 
                _animator.ResetTrigger(__jump);
            }

            // Reset the input character Y rotation
            _turningDirection = 0;

            if (!_canMove) {
                _movementDirection.x = _movementDirection.z = 0;

                if (IsClimbing()) {
                    _movementDirection.y = 0;
                } else if (SlidingCheckNeeded()) {
                    // Still apply sliding
                    ApplySliding();
                }

                if (_canRotate) {
                    _facingDirection = Utils.ProjectOnHorizontalPlane(GetFacingDirection());
                }
            } else if (_climbingState == ClimbingState.ClimbingUpLedge) {
                HandleLedgeClimbUp();
            } else if (_climbingState == ClimbingState.GrabbingLedge) {
                HandleLedgeMovement();
            } else if (_climbingState == ClimbingState.FreeClimbing) {
                HandleFreeClimbing();
            } else if (_grounded || _3dMovementEnabled) {
                // Grounded and 3D movement behavior
                HandleStandardMovement();
            } else {
                HandleMidairMovement();
            }

            if (ApplyGravity()) {
                _movementDirection.y -= Gravity * Time.deltaTime;
            }

            // Move the character
            Move(_movementDirection * Time.deltaTime);

            if (_canRotate) {
                // Rotate the character
                RotateToFacingDirection();
            }

            // Pass values to the animator
            SetValuesInAnimator();

            _currentState = DetermineCurrentState();

            _allowMidairMovement = false;
        }

        /// <summary>
        /// Gets the movement direction based on the controller input in world space 
        /// </summary>
        /// <returns>The direction the character should move in with a maximum magnitude of 1</returns>
        protected abstract Vector3 GetMovementDirection();

        /// <summary>
        /// Gets the climbing direction based on the controller input in world space. Note that the z axis will be taken for climbing up/down
        /// </summary>
        /// <returns>The direction the character should climb in with a maximum magnitude of 1</returns>
        protected abstract Vector3 GetClimbingDirection();

        /// <summary>
        /// Gets the target facing direction
        /// </summary>
        /// <returns>Direction the character should face eventually</returns>
        protected abstract Vector3 GetFacingDirection();

        /// <summary>
        /// Gets the movement speed
        /// </summary>
        /// <returns>Movement speed to apply to the movement direction</returns>
        protected abstract float GetStandardMovementSpeed();

        /// <summary>
        /// Applies acceleration to the given current speed until the given target speed is reached. Holds for positive and negative changes 
        /// </summary>
        /// <param name="currentSpeed">Current speed to accelerate/decelerate</param>
        /// <param name="targetSpeed">Target speed to reach as the result of an acceleration/deceleration</param>
        /// <returns></returns>
        protected virtual float ApplyAcceleration(float currentSpeed, float targetSpeed) {
            if (targetSpeed == 0) {
                return 0;
            }

            if (SteadyAcceleration) {
                //if (_swimming && IsCloseToWaterSurface()) {
                //    return targetMovementSpeed;
                //} else {
                if (targetSpeed > _movementSpeed) {
                    return Mathf.Min(targetSpeed, _movementSpeed + Acceleration * Time.deltaTime);
                } else {
                    return Mathf.Max(targetSpeed, _movementSpeed - Acceleration * Time.deltaTime);
                }
                //}
            } else {
                return Mathf.SmoothDamp(_movementSpeed, _movementDirection.magnitude * targetSpeed, ref _accelerationVelocity, /*_swimming && IsCloseToWaterSurface() ? 0 :*/ AccelerationTime);
            }
        }

        /// <summary>
        /// Handles the logic for climbing/pulling up ledges
        /// </summary>
        protected virtual void HandleLedgeClimbUp() {
            _movementDirection = Vector3.zero;
            if (Vector3.Distance(transform.position, _lastLedgeClimbUpPosition) < 0.05f * Time.deltaTime) {
                // No progress => count up
                _ledgeClimbUpStuckTimer += Time.deltaTime;
            }

            if (_ledgeClimbUpStuckTimer > LedgeClimbingUpTimeout) {
                // Timeout reached => stop climbing
                StopClimbingUpLedge();
            } else {
                // Continue climbing up
                float movementSpeed = ApplyMovementSpeedMultipliers(LedgeClimbingUpSpeed);
                if (GetColliderBottom().y < _grabbedLedgeHeight) {
                    // Climb up
                    _movementDirection = Vector3.up * movementSpeed;
                } else if (!IsStandingOnLedge()) {
                    // Go forward
                    _movementDirection = transform.forward * movementSpeed;
                } else {
                    // Done
                    StopClimbingUpLedge();
                }
                // Store the current position to track process
                _lastLedgeClimbUpPosition = transform.position;
            }
        }

        /// <summary>
        /// Handles ledge movement logic
        /// </summary>
        protected virtual void HandleLedgeMovement() {
            _movementDirection = Vector3.zero;
            _ledgeGrabTimer += Time.deltaTime;

            RaycastHit ledgeHit;
            RaycastHit cliffHit;
            Vector3 right;

            #region Alignment with the current ledge
            // Check the ledge directly in front of the character
            if (CheckLedgeGrabPossibility(GetReachOutVectorForward(), out ledgeHit, out cliffHit)) {
                AlignWithLedge(ref ledgeHit, ref cliffHit);
                right = Vector3.Cross(cliffHit.normal, Vector3.up);
                _grabbedLedgeHeight = ledgeHit.point.y + 0.05f; // small buffer
            } else {
                // No longer a grabbable ledge there => stop
                StopGrabbingLedge();
                return;
            }
            #endregion

            #region Movement along the ledge
            Vector3 localMovementDirection = transform.InverseTransformDirection(GetClimbingDirection());
            if (localMovementDirection.x < 0) {
                // Check if movement to the left is possible
                bool ledgeLeft = CheckLedgeGrabPossibility(GetReachOutVectorLeft(), out ledgeHit, out cliffHit);
                if (ledgeLeft) {
                    _movementDirection += right * localMovementDirection.x;
                }
            } else if (localMovementDirection.x > 0) {
                // Check if movement to the right is possible
                bool ledgeRight = CheckLedgeGrabPossibility(GetReachOutVectorRight(), out ledgeHit, out cliffHit);
                if (ledgeRight) {
                    _movementDirection += right * localMovementDirection.x;
                }
            }
            _movementDirection *= ApplyMovementSpeedMultipliers(ClimbingSpeed);
            #endregion

            #region Actions
            if (ShouldClimbUpLedge(ref localMovementDirection) && AllowClimbingUpLedge(ref ledgeHit)) {
                // Start climbing up the ledge
                StopGrabbingLedge();
                _climbingState = ClimbingState.ClimbingUpLedge;
            } else if (ShouldLetGoOfLedge(ref localMovementDirection)) {
                // Let go of the ledge
                StopGrabbingLedge();
            }
            #endregion
        }

        /// <summary>
        /// Handles free climbing logic, i.e. climbing along "walls" in all directions
        /// </summary>
        protected virtual void HandleFreeClimbing() {
            _movementDirection = Vector3.zero;
            bool grabbedLedge = false;
            if (AllowLedgeGrabbing()) {
                grabbedLedge = TryLedgeGrab(out RaycastHit ledgeHit);
            }

            if (!grabbedLedge) {
                // Continue free climbing
                Vector3 up = Vector3.up;
                Vector3 right = Vector3.zero;

                #region Alignment with the currently climbed collider
                // Check the collider directly in front of the character
                if (CheckFreeClimbingPossibility(transform.position, out RaycastHit cliffHit, out _)) {
                    AlignWithCliff(ref cliffHit);
                    Vector3 cliffHitNormal = cliffHit.normal;
                    Vector3.OrthoNormalize(ref cliffHitNormal, ref up);
                    right = Vector3.Cross(cliffHit.normal, up);
                } else {
                    StopFreeClimbing();
                }
                #endregion

                #region Movement
                Vector3 localMovementDirection = transform.InverseTransformDirection(GetClimbingDirection());
                if (localMovementDirection.x != 0 || localMovementDirection.z != 0) {
                    _movementDirection += right * localMovementDirection.x;
                    _movementDirection += up * localMovementDirection.z; // take z for up and down
                    _movementDirection *= ApplyMovementSpeedMultipliers(ClimbingSpeed);

                    Vector3 desiredPosition = transform.position + _movementDirection.normalized * (EnableLedgeClimbing ? _colliderBottomOffset + _characterController.GetActualHeight() - LedgeGrabHeight : _characterController.GetActualRadius());
                    if (CheckFreeClimbingPossibility(desiredPosition, out _, out bool groundDetected)) {
                        // Climbing possible
                        if (groundDetected && localMovementDirection.z < -0.5f) {
                            // Character should come down to the ground again
                            StopFreeClimbing();
                        }
                    } else {
                        // Climbing to desired position not possible
                        _movementDirection = Vector3.zero;
                    }
                }
                #endregion
            }
        }

        /// <summary>
        /// Handles standard movement, i.e. grounded and 3D movement (swimming or flying)
        /// </summary>
        protected virtual void HandleStandardMovement() {
            // Reset the counter for the number of remaining midair jumps
            _midairJumpsCount = 0;
            // Reset the counter for the number of remaining midair moves
            _midairMovesCount = 0;
            // Reset the running jump flag
            _runningJump = false;

            if (_autorunning) {
                _inputDirection.z = 1.0f;
            }

            _movementDirection = GetMovementDirection();

            _facingDirection = GetFacingDirection();

            float maxMovementSpeed = GetStandardMovementSpeed();
            _movementSpeed = ApplyAcceleration(_movementSpeed, _movementDirection.magnitude * maxMovementSpeed);

            // Apply the resulting movement speed
            _movementDirection = _movementDirection.normalized * _movementSpeed;

            if (!_3dMovementEnabled) {
                // Simple grounded movement => keep the already computed movement and facing direction
                _movementDirection.y = -FallingThreshold;

                if (_jump) {
                    // The character is not swimming and should jump this frame
                    PerformJump();
                }

                if (SlidingCheckNeeded()) {
                    // Apply sliding
                    ApplySliding();
                }
            } else {
                // 3D movement enabled (swimming or flying)
                if (_swimming) {
                    bool closeToWaterSurface = IsCloseToWaterSurface();

                    if (closeToWaterSurface) {
                        if (EnableSwimmingJumps && _surface) {
                            if (_animator) {
                                _animator.SetTrigger(__jump);
                            }
                        } else {
                            PreventSwimmingAboveSurface();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles midair movement
        /// </summary>
        protected virtual void HandleMidairMovement() {
            bool grabbedLedge = false;
            if (AllowLedgeGrabbing()) {
                grabbedLedge = TryLedgeGrab(out _);
            }

            if (!grabbedLedge) {
                if (_allowMidairMovement) {
                    // Movement while airborne is possible during this frame
                    _allowMidairMovement = false;

                    if (EnableMidairMovement == MidairMovement.Always
                        || EnableMidairMovement == MidairMovement.OnlyAfterStandingJump && !_runningJump) {
                        if (_runningJump) {
                            _inputDirection.z = 0;
                        }

                        Vector3 movementDirection = GetMovementDirection();
                        // Apply the airborne speed
                        movementDirection *= MidairSpeed;

                        if (_runningJump) {
                            _movementDirection.x += movementDirection.x;
                            _movementDirection.z += movementDirection.z;
                        } else {
                            _movementDirection.x = movementDirection.x;
                            _movementDirection.z = movementDirection.z;
                        }
                    }
                }

                _facingDirection = Utils.ProjectOnHorizontalPlane(GetFacingDirection());

                if (_jump) {
                    PerformJump();
                }
            }
        }

        /// <summary>
        /// Rotates the character to the facing direction
        /// </summary>
        protected virtual void RotateToFacingDirection() {
            if (!IsLookingInDirection(_facingDirection)) {
                float turningDirection = transform.rotation.eulerAngles.y;

                Vector3 upwards = _3dMovementEnabled && (_surface || _dive) ? Vector3.Cross(_facingDirection, transform.right) : Vector3.up;

                Quaternion targetRotation = Quaternion.LookRotation(_facingDirection, upwards);
                // Smoothly rotate to the target rotation
                float delta = Quaternion.Angle(transform.rotation, targetRotation);
                if (delta >= 180.0f) {
                    // Decrease delta under 180 so that SmoothDampAngle decreases
                    delta = 179.0f;
                }
                float t = Mathf.SmoothDampAngle(delta, 0, ref _rotationVelocity, AlignmentTime);
                t = 1.0f - t / delta;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);

                turningDirection = transform.rotation.eulerAngles.y - turningDirection;

                if (turningDirection < -180.0f) {
                    turningDirection += 360.0f;
                }

                _turningDirection += turningDirection;
            } else {
                _alignmentInProgress = false;
            }
        }

        /// <summary>
        /// Checks if the character is grounded
        /// </summary>
        /// <returns>True if it is grounded, otherwise false</returns>
        protected virtual bool GroundedCheck() {
            if (_slidingBuffer < -AntiStuckTimeout && _characterController.velocity.magnitude < 0.05f) {
                _antiStuckEnabled = true;
            }

            if (_antiStuckEnabled) {
                // Anti-stuck mechanic is enabled => behave like if the character was grounded
                return true;
            } else if (_movementDirection.y < 0 || _swimming) {
                // Check for grounded only if we are drawn towards the ground
                return GroundedCheckSphere(_colliderBottom.Center + Vector3.down * GroundedTolerance);
            } else {
                // Upward movement, e.g. jumping => disable grounded check so that colliders from the side cannot interrupt the movement
                return false;
            }
        }

        /// <summary>
        /// Perfoms a check sphere at the given sphere center
        /// </summary>
        /// <param name="sphereCenter">The center of the check sphere</param>
        /// <returns>True if grounded, otherwise false</returns>
        protected virtual bool GroundedCheckSphere(Vector3 sphereCenter) {
            return Physics.CheckSphere(sphereCenter, _colliderBottom.Radius, ~IgnoredLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Checks if the character should swim during this frame. Also sets the internal variables _swimmingStartHeight and _currentWaterHeight accordingly
        /// </summary>
        /// <returns>True if it should swim, otherwise false</returns>
        protected virtual bool SwimmingCheck() {
            _swimmingStartHeight = transform.position.y + SwimmingStartHeight;
            _currentWaterHeight = GetCurrentWaterHeight();
            return _swimmingStartHeight <= _currentWaterHeight;
        }

        /// <summary>
        /// Signals the motor to try a midair move 
        /// </summary>
        public virtual void TryMidairMovement() {
            if (EnableMidairMovement == MidairMovement.Never) {
                return;
            }

            if (UnlimitedMidairMoves) {
                _allowMidairMovement = true;
                return;
            }

            // Allow midair movement for the current frame and increase the midair moves counter
            if (_midairMovesCount < AllowedMidairMoves) {
                _allowMidairMovement = true;
                _midairMovesCount++;
            }
        }

        /// <summary>
        /// Checks if a sliding check is needed in this frame
        /// </summary>
        /// <returns>True if a sliding check should be performed, otherwise false</returns>
        protected virtual bool SlidingCheckNeeded() {
            return EnableSliding && !_swimming;
        }

        /// <summary>
        /// Checks if the character is allowed to grab a ledge
        /// </summary>
        /// <returns>True if ledge grabbing is allowed, otherwise false</returns>
        protected virtual bool AllowLedgeGrabbing() {
            return EnableLedgeClimbing
                    && (!OnlyAllowLedgeGrabbingWhileFalling || IsFalling())
                    && !_ledgeGrabbingOnCooldown
                    && transform.root == transform; // not being transported 
        }

        /// <summary>
        /// Checks if the character is allowed to climb up a ledge
        /// </summary>
        /// <param name="ledgeHit">Ledge hit to check</param>
        /// <param name="drawGizmos">If additionally gizmos should be drawn</param>
        /// <returns>True if the climbing up the hit ledge is allowed, otherwise false</returns>
        protected virtual bool AllowClimbingUpLedge(ref RaycastHit ledgeHit, bool drawGizmos = false) {
            bool result = true;
            if (_ledgeGrabTimer < 0.5f) {
                result = false;
            }
            Vector3 start = transform.InverseTransformPoint(ledgeHit.point);
            start.x = 0;
            start = transform.TransformPoint(start);
            start.y = _grabbedLedgeHeight + _characterController.GetActualRadius() + 0.5f * _characterController.GetActualRadius() * Mathf.Tan(Mathf.Deg2Rad * Vector3.Angle(ledgeHit.normal, Vector3.up));

            Vector3 dir = Vector3.up * (_characterController.GetActualHeight() - 2.0f * _characterController.GetActualRadius());
            result = result && !Physics.CheckCapsule(start, start + dir, _characterController.GetActualRadius(), ~IgnoredLayers, QueryTriggerInteraction.Ignore);

            if (DrawLedgeCheckGizmos && drawGizmos) {
                DrawSphereCast(start, Vector3.up * dir.magnitude, _characterController.GetActualRadius(), result);
            }
            return result;
        }

        /// <summary>
        /// Starts the ledge grab cooldown (coroutine)
        /// </summary>
        protected virtual void StartLedgeGrabCooldown() {
            _ledgeGrabTimer = 0;

            if (_ledgeGrabbingCooldown != null) {
                StopCoroutine(_ledgeGrabbingCooldown);
            }

            _ledgeGrabbingCooldown = LedgeGrabCooldownCoroutine();
            StartCoroutine(_ledgeGrabbingCooldown);
        }

        /// <summary>
        /// Cooldown coroutine for ledge grabbing
        /// </summary>
        protected virtual IEnumerator LedgeGrabCooldownCoroutine() {
            _ledgeGrabbingOnCooldown = true;
            yield return new WaitForSeconds(LedgeGrabCooldown);
            _ledgeGrabbingOnCooldown = false;
            _ledgeGrabbingCooldown = null;
        }

        /// <summary>
        /// Starts the free climbing cooldown (coroutine)
        /// </summary>
        protected virtual void StartFreeClimbingCooldown() {
            StartCoroutine(FreeClimbingCooldownCoroutine());
        }

        /// <summary>
        /// Cooldown coroutine for free climbing
        /// </summary>
        protected virtual IEnumerator FreeClimbingCooldownCoroutine() {
            _freeClimbingOnCooldown = true;
            yield return new WaitForSeconds(FreeClimbingCooldown);
            _freeClimbingOnCooldown = false;
        }

        /// <summary>
        /// Checks if the character is allowed to free climb
        /// </summary>
        /// <returns>True if free climbing is allowed, otherwise false</returns>
        protected virtual bool AllowFreeClimbing() {
            return EnableFreeClimbing
                    && (!OnlyAllowFreeClimbingWhileGrounded || _grounded)
                    && IsInMotion()
                    && _climbingState != ClimbingState.FreeClimbing
                    && _climbingState != ClimbingState.ClimbingUpLedge
                    && !_freeClimbingOnCooldown
                    && transform.root == transform; // not being transported
        }

        /// <summary>
        /// Gets the sphere cast check distance for climbing (ledge grabbing and free climbing)
        /// </summary>
        /// <param name="angle">Maximum climbing angle difference</param>
        /// <returns>Distance for covering all angles until angle</returns>
        protected virtual float GetClimbingCheckDistance(float angle) {
            return 0.5f * GrabRange * Mathf.Tan(Mathf.Deg2Rad * angle);
        }

        /// <summary>
        /// Try to grab a ledge in range
        /// </summary>
        /// <param name="ledgeHit">A ledge in range, otherwise default(RaycastHit)</param>
        /// <returns>True if a ledge is grabbed, otherwise false</returns>
        protected virtual bool TryLedgeGrab(out RaycastHit ledgeHit) {
            bool result = CheckLedgeGrabPossibility(GetReachOutVectorForward(), out ledgeHit, out RaycastHit cliffHit);
            if (result) {
                if (!IsClimbing() && Vector3.Angle(Vector3.up, ledgeHit.normal) > LedgeGrabMaxSlope) {
                    // Ledge too steep
                    return false;
                }
                _climbingState = ClimbingState.GrabbingLedge;
                AlignWithLedge(ref ledgeHit, ref cliffHit);
            }
            return result;
        }

        /// <summary>
        /// Gets the reach out vector in forward direction
        /// </summary>
        /// <returns>Reach out vector in world coordinates</returns>
        protected virtual Vector3 GetReachOutVectorForward() {
            return GetReachOutVector(0);
        }

        /// <summary>
        /// Gets the reach out vector in forward/left direction
        /// </summary>
        /// <returns>Reach out vector in world coordinates</returns>
        protected virtual Vector3 GetReachOutVectorLeft() {
            return GetReachOutVector(-1);
        }

        /// <summary>
        /// Gets the reach out vector in forward/right direction
        /// </summary>
        /// <returns>Reach out vector in world coordinates</returns>
        protected virtual Vector3 GetReachOutVectorRight() {
            return GetReachOutVector(1);
        }

        /// <summary>
        /// Gets a reach out vector depending on the given sign
        /// </summary>
        /// <param name="sign">Direction to reach out</param>
        /// <returns>Reach out vector in world coordinates</returns>
        protected virtual Vector3 GetReachOutVector(float sign) {
            return transform.forward + sign * (transform.right * GrabRange * 0.5f);
        }

        /// <summary>
        /// Aligns the character with the given ledge
        /// </summary>
        /// <param name="ledgeHit">Hit of the ledge (top)</param>
        /// <param name="cliffHit">Hit of the corresponding cliff (horizontal)</param>
        protected virtual void AlignWithLedge(ref RaycastHit ledgeHit, ref RaycastHit cliffHit) {
            AlignWithCliff(ref cliffHit);
            float deltaToLedge = ledgeHit.point.y - transform.position.y - LedgeGrabHeight; // vertical only            
            _movementDirection.y = deltaToLedge * 20.0f;
        }

        /// <summary>
        /// Checks if there is the possiblity to grab a ledge in the given direction
        /// </summary>
        /// <param name="reachOutDirection">Direction to reach out for a ledge</param>
        /// <param name="ledgeHit">Hit ledge if any</param>
        /// <param name="cliffHit">Hit of the cliff below the ledge if any</param>
        /// <param name="drawGizmos">Additionally draw ledge grab check gizmos</param>
        /// <returns>True if the character can grab a ledge in the given direction, otherwise false</returns>
        protected virtual bool CheckLedgeGrabPossibility(Vector3 reachOutDirection, out RaycastHit ledgeHit, out RaycastHit cliffHit, bool drawGizmos = false) {
            bool result = false;
            ledgeHit = default;
            ledgeHit.distance = Mathf.Infinity;
            cliffHit = default;

            float distance = GetClimbingCheckDistance(LedgeGrabMaxSlope);
            float actualGrabDistance = _characterController.GetActualRadius() + LedgeGrabDistance + _cliffAlignmentOffset.magnitude;
            Vector3 ledgeCheckStart = transform.position + Vector3.up * (LedgeGrabHeight + distance + ClimbingCheckRadius) + reachOutDirection * actualGrabDistance;
            // Double the check distance if we are already grabbing a ledge to cover the full distance (ledge up and down) for the max ledge grab angle
            distance = IsClimbing() ? 2.0f * distance : distance;
            Ray ledgeRay = new Ray(ledgeCheckStart, Vector3.down);
            RaycastHit[] ledgeHits = Physics.SphereCastAll(ledgeRay, ClimbingCheckRadius, distance, ClimbableLayers, QueryTriggerInteraction.Ignore);

            bool ledgeWasHit = false;
            foreach (RaycastHit hit in ledgeHits) {
                if (hit.point == Vector3.zero) {
                    // Collider overlaps at the start => ledge too high to grab
                    ledgeWasHit = false;
                    result = false;
                    break;
                }

                // Find closest hit
                if (hit.distance < ledgeHit.distance) {
                    ledgeWasHit = true;
                    ledgeHit = hit;
                }
            }

            if (ledgeWasHit) {
                // Ledge found => check corresponding cliff (below the ledge)
                Vector3 cliffCheckStart = transform.position;
                cliffCheckStart.y = ledgeHit.point.y - ClimbingCheckRadius;

                result = Physics.SphereCast(cliffCheckStart, ClimbingCheckRadius, reachOutDirection, out cliffHit, actualGrabDistance, ClimbableLayers, QueryTriggerInteraction.Ignore);
                if (drawGizmos && !result) {
                    DrawSphereCast(cliffCheckStart, reachOutDirection * actualGrabDistance, ClimbingCheckRadius, result);
                }
            }

            if (drawGizmos) {
                DrawSphereCast(ledgeRay.origin, ledgeRay.direction * distance, ClimbingCheckRadius, ledgeWasHit);
            }
            return result;
        }

        /// <summary>
        /// Checks if the character is standing on the ledge it started to climb up
        /// </summary>
        /// <returns>True if the character is grounded, otherwise false</returns>
        protected virtual bool IsStandingOnLedge() {
            return Physics.Raycast(GetColliderBottom(), Vector3.down, 1.0f, ~IgnoredLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Try to start free climbing
        /// </summary>
        protected virtual void TryFreeClimbing() {
            if (CheckFreeClimbingPossibility(transform.position, out RaycastHit ignore, out bool groundDetected)) {
                _climbingState = ClimbingState.FreeClimbing;
            }
        }

        /// <summary>
        /// Aligns the character with the given cliff
        /// </summary>
        /// <param name="cliffHit">Hit of the corresponding cliff (horizontal)</param>
        protected virtual void AlignWithCliff(ref RaycastHit cliffHit) {
            _facingDirection = -cliffHit.normal;

            if (cliffHit.normal.y > 0) {
                // Cliff towards the character, i.e. the character collider might not be directly at the cliff
                Vector3 deltaToCliff = GetRaycastHitDirection(ref cliffHit); // horizontal delta
                SetCliffAlignmentOffsetOnce(new Vector3(deltaToCliff.x, 0, deltaToCliff.z));
            }
        }

        /// <summary>
        /// Checks if there is the possiblity to free climb at the given position
        /// </summary>
        /// <param name="desiredPosition">Target position to check</param>
        /// <param name="cliffHit">Cliff hit which results from moving to the desired position</param>
        /// <param name="groundDetected">True if the character touches the ground at the desired position</param>
        /// <param name="drawGizmos">Additionally draw free climbing check gizmos</param>
        /// <returns>True if the character can climb to the desired position, otherwise false</returns>
        protected virtual bool CheckFreeClimbingPossibility(Vector3 desiredPosition, out RaycastHit cliffHit, out bool groundDetected, bool drawGizmos = false) {
            groundDetected = false;
            cliffHit = default;
            Vector3 up = transform.up;
            Vector3 right = transform.right * 0.5f * GrabRange;

            // Check both hands and feet for climbing possibility
            if (!(FreeClimbingSphereCast(desiredPosition + up * FreeClimbingHandsHeight - right, out RaycastHit cliffHitLeftHand, drawGizmos)
                & FreeClimbingSphereCast(desiredPosition + up * FreeClimbingHandsHeight + right, out RaycastHit cliffHitRightHand, drawGizmos)
                & FreeClimbingSphereCast(desiredPosition + up * FreeClimbingFeetHeight - right, out RaycastHit cliffHitLeftFoot, drawGizmos)
                & FreeClimbingSphereCast(desiredPosition + up * FreeClimbingFeetHeight + right, out RaycastHit cliffHitRightFoot, drawGizmos))) {
                return false;
            }

            if (!EnableLedgeClimbing
                && !(Utils.IsAlmostEqual(cliffHitLeftHand.normal, cliffHitLeftFoot.normal, 5.0f)
                    && Utils.IsAlmostEqual(cliffHitRightHand.normal, cliffHitRightFoot.normal, 5.0f))) {
                // Prevent free climbing sideways over ledges
                return false;
            }

            // Check if the ground is nearby the desired climbing position
            groundDetected = GroundedCheckSphere(desiredPosition + Vector3.up * (_colliderBottom.Radius - GroundedTolerance));
            // Sphere cast again to get a proper cliff hit directly in front of the character
            FreeClimbingSphereCast(desiredPosition + up * FreeClimbingHandsHeight, out cliffHit, false);

            return true;
        }

        /// <summary>
        /// Checks if a free climbing sphere cast from the given origin is successful. Can be imagined as a free climbing grab check
        /// </summary>
        /// <param name="origin">Location to start the sphere cast from</param>
        /// <param name="cliffHit">The hit cliff if any</param>
        /// <param name="drawGizmos">Additionally draw free climbing check gizmos</param>
        /// <returns>The result of the sphere cast</returns>
        protected virtual bool FreeClimbingSphereCast(Vector3 origin, out RaycastHit cliffHit, bool drawGizmos = false) {
            float actualGrabDistance = GetClimbingCheckDistance(FreeClimbingMaxCornerAngle) + _characterController.GetActualRadius() - ClimbingCheckRadius;
            Ray ray = new Ray(origin, transform.forward);
            bool result = Physics.SphereCast(ray, ClimbingCheckRadius, out cliffHit, actualGrabDistance, ClimbableLayers, QueryTriggerInteraction.Ignore);
            if (drawGizmos) {
                DrawSphereCast(ray.origin, ray.direction * actualGrabDistance, ClimbingCheckRadius, result);
            }

            return result;
        }

        /// <summary>
        /// Sets the cliff alignment offset without overwriting the previous value. Only exception is Vector3.zero which resets the alignment offset
        /// </summary>
        /// <param name="offset">The offset to set. If Vector3.zero, the offset is reset</param>
        protected virtual void SetCliffAlignmentOffsetOnce(Vector3 offset) {
            if (_cliffAlignmentOffset == Vector3.zero) {
                _cliffAlignmentOffset = offset;
            }
        }

        /// <summary>
        /// Gets the direction from the character to the given hit in the horizontal plane and world coordinates
        /// </summary>
        /// <param name="hit">Target hit</param>
        /// <returns>A vector going from the character collider to the given hit</returns>
        public virtual Vector3 GetRaycastHitDirection(ref RaycastHit hit) {
            return Utils.ProjectOnHorizontalPlane(hit.point - transform.position) * (hit.distance - _characterController.GetActualRadius());
        }

        /// <summary>
        /// Checks if the character should climb up the ledge it currently holds on to
        /// </summary>
        /// <param name="localMovementDir">Movement input in character local coordinates</param>
        /// <returns>True if the character should climb up, otherwise false</returns>
        protected virtual bool ShouldClimbUpLedge(ref Vector3 localMovementDir) {
            return localMovementDir.z > 0.5f; // take z for down
        }

        /// <summary>
        /// Checks if the character should let go the ledge it currently holds on to
        /// </summary>
        /// <param name="localMovementDir">Movement input in character local coordinates</param>
        /// <returns>True if the character should let go, otherwise false</returns>
        protected virtual bool ShouldLetGoOfLedge(ref Vector3 localMovementDir) {
            return localMovementDir.z < -0.5f; // take z for up
        }

        /// <summary>
        /// The character lets go of the ledge it currently holds on to
        /// </summary>
        /// <param name="tryFreeClimbing">If true, a transition to free climbing is tried</param>
        protected virtual void StopGrabbingLedge(bool tryFreeClimbing = true) {
            ResetClimbingVariables();
            StartLedgeGrabCooldown();

            if (tryFreeClimbing) {
                TryFreeClimbing();
            }
        }

        /// <summary>
        /// Stops climbing up the ledge 
        /// </summary>
        protected virtual void StopClimbingUpLedge() {
            ResetClimbingVariables();
            _ledgeClimbUpStuckTimer = 0;
            StartLedgeGrabCooldown();
            StartFreeClimbingCooldown();
        }

        /// <summary>
        /// Stops free climbing
        /// </summary>
        protected virtual void StopFreeClimbing() {
            ResetClimbingVariables();
            StartFreeClimbingCooldown();
        }

        /// <summary>
        /// Resets the common climbing variables (ledge and free climbing)
        /// </summary>
        protected virtual void ResetClimbingVariables() {
            _climbingState = ClimbingState.None;
            _cliffAlignmentOffset = Vector3.zero;
        }

        /// <summary>
        /// Cancels climbing (ledge grab or free climbing)
        /// </summary>
        public virtual void CancelClimbing() {
            if (!IsClimbing()) {
                return;
            }

            switch (_climbingState) {
                case ClimbingState.FreeClimbing:
                    StopFreeClimbing();
                    break;
                case ClimbingState.GrabbingLedge:
                    StopGrabbingLedge(false);
                    break;
                case ClimbingState.ClimbingUpLedge:
                    StopClimbingUpLedge();
                    break;
            }
        }

        /// <summary>
        /// Gets the bottom of the character controller collider capsule
        /// </summary>
        /// <returns>Position of the collider bottom in world coordinates</returns>
        protected virtual Vector3 GetColliderBottom() {
            return transform.position + Vector3.up * _colliderBottomOffset;
        }

        /// <summary>
        /// Copies the animator parameter values which were also set in the internal animator
        /// </summary>
        /// <param name="animator">Target animator where the parameters will be set</param>
        public virtual void CopyAnimatorValuesTo(Animator animator) {
            SetValuesInAnimator(animator);
        }

        /// <summary>
        /// Sets the internally computed parameter values in the given animator. If null is passed, the internal animator is used
        /// </summary>
        /// <param name="animator">Target animator where the parameters will be set</param>
        protected virtual void SetValuesInAnimator(Animator animator = null) {
            if (!animator) {
                animator = _animator;
            }

            Vector3 localMovementDir = transform.InverseTransformVector(_movementDirection.normalized);
            if (Is3dMovementEnabled() && Mathf.Abs(localMovementDir.z) < 0.1f) {
                // Account for imperfect rotations back to the horizontal plane
                localMovementDir.z = 0;
            } else if (IsClimbing()) {
                // Swap input direction for the animator
                localMovementDir.z = localMovementDir.y;
            }
            localMovementDir.y = 0;
            localMovementDir.Normalize();

            float movementSpeed = GetNormalizedMovementSpeed();
            if (movementSpeed > 1.0f) {
                // Speed is greater than the maximum possible motor speed => adjust the animator speed
                animator.speed = movementSpeed;
            } else {
                // Keep the normal speed
                animator.speed = 1.0f;
            }

            // Local Movement X -> normalized
            animator.SetFloat(__localMovementX, localMovementDir.x, 0.05f, Time.deltaTime);
            // Local Movement Z -> normalized
            animator.SetFloat(__localMovementZ, localMovementDir.z, 0.05f, Time.deltaTime);
            // Movement Speed
            animator.SetFloat(__movementSpeed, _movementSpeed);
            // Turning Direction
            animator.SetFloat(__turningDirection, _turningDirection, 0.1f, Time.deltaTime);
            animator.SetBool(__grounded, _grounded);
            // Crouching
            animator.SetBool(__crouching, _crouching);
            animator.SetBool(__falling, IsFalling());
            animator.SetBool(__sliding, _slidingBuffer < -SlidingTimeout); // buffer full due to many consecutive sliding events => fire animation
            animator.SetBool(__swimming, _swimming);
            animator.SetBool(__flying, _flying);
            animator.SetInteger(__climbingState, (int)_climbingState);

            if (animator != _animator && _animator.GetBool(__jump)) {
                animator.SetTrigger(__jump);
            }
        }

        /// <summary>
        /// Updates the animator state immediately, e.g. if parameters were set late
        /// </summary>
        public virtual void UpdateAnimator() {
            _animator.Update(0);
        }

        /// <summary>
        /// Gets the normalized movement speed, i.e. a value from the interval [0;1]
        /// </summary>
        /// <returns>The normalized movement speed</returns>
        protected virtual float GetNormalizedMovementSpeed() {
            float maxSpeed = RunSpeed * SprintSpeedMultiplier;
            return _movementSpeed / maxSpeed;
        }

        /// <summary>
        /// Immediately translates the character
        /// </summary>
        /// <param name="translation">Translation vector</param>
        protected virtual void Translate(Vector3 translation, bool suppressWarning = false) {
            if (translation == Vector3.zero) {
                return;
            }

            if (!Time.inFixedTimeStep) {
                if (!suppressWarning) {
                    Debug.LogWarning("Method Translate was called outside of FixedUpdate! This can lead to unexpected side effects (double-click here for more information)");
                }
                // Not called from within FixedUpdate => disable the character controller component so that it does not overwrite the target position
                // The disadvantage is that collision detection callbacks are called again (OnTriggerEnter) or never (OnTriggerExit)
                _characterController.enabled = false;
            }

            transform.position += translation;
            UpdateColliderBottom();

            if (!Time.inFixedTimeStep) {
                // Enable the character controller again
                _characterController.enabled = true;
            }
        }

        /// <summary>
        /// Applies passive movement when other colliders intersect with the character controller collider
        /// </summary>
        protected virtual void ApplyCollisionMovement() {
            Vector3 moveDirection = Vector3.zero;

            Vector3 start = _colliderBottom.Center;
            Vector3 end = start + Vector3.up * (_characterController.GetActualHeight() - 2.0f * _characterController.GetActualRadius());
            Collider[] colliders = Physics.OverlapCapsule(start, end, _characterController.GetActualRadius(), ~IgnoredLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider c in colliders) {
                if (c is MeshCollider mc && !mc.convex || c is TerrainCollider) {
                    // Both do not support the "ClosestPoint" method => skip
                    continue;
                }

                // Get the point which is closest to the character, i.e. deepest inside the character collider
                Vector3 closestPoint = c.ClosestPoint(transform.position);
                // Project the closest point to the same height as the character
                closestPoint.y = transform.position.y;
                Vector3 closestPointDirection = closestPoint - transform.position;
                // Add delta movement needed to move the character collider out of collider c 
                moveDirection += closestPointDirection.normalized * (closestPointDirection.magnitude - _characterController.GetActualRadius());
            }

            if (moveDirection != Vector3.zero) {
                // Apply the combined delta movements
                Translate(moveDirection);
            }
        }

        /// <summary>
        /// Checks if gravity should be applied to the character
        /// </summary>
        protected virtual bool ApplyGravity() {
            return !_3dMovementEnabled && !IsClimbing();
        }

        /// <summary>
        /// Calculates the jump height based on the gravity
        /// </summary>
        /// <returns>Resulting jump height</returns>
        protected virtual float CalculateJumpHeight() {
            return Mathf.Sqrt(2 * JumpHeight * Gravity);
        }

        /// <summary>
        /// Immediately resets the X and Z axes rotations of the character
        /// </summary>
        protected virtual void ResetXandZrotations() {
            // The character controller collider is not rotatable, i.e. it can happen that it touches earlier the ground than the rotated character
            // Therefore, we have to "teleport" the character to the character controller bottom, e.g. when landing from flying or surfacing from swimming to grounded
            Vector3 delta = _colliderBottom.Center + Vector3.down * (_colliderBottom.Radius + _colliderBottomOffset) - transform.position;
            Translate(delta);
            // Now rotate the character
            transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        }

        /// <summary>
        /// Applies sliding to the character if it is standing on too steep terrain
        /// </summary>
        protected virtual void ApplySliding() {
            Vector3 summedNormals = Vector3.zero;

            RaycastHit[] hits = Physics.SphereCastAll(_colliderBottom.Center, _colliderBottom.Radius, Vector3.down, GroundedTolerance, ~IgnoredLayers, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit hit in hits) {
                // Check if the slope is too steep
                if (SlopeTooSteep(hit.normal, out _)) {
                    // Add normal that would cause sliding to previous ones
                    summedNormals += hit.normal;
                } else {
                    ResetSliding();
                    // Turn off the anti-stuck mode
                    _antiStuckEnabled = false;
                    return;
                }
            }

            if (SlopeTooSteep(summedNormals, out float slope)) {
                // Update the buffer
                _slidingBuffer -= Time.deltaTime;

                if (_antiStuckEnabled) {
                    ResetSliding();
                } else if (_slidingBuffer < 0) {
                    // Buffer is depleted
                    _sliding = true;
                    // Compute the sliding direction
                    Vector3 slidingDirection = new Vector3(summedNormals.x, -summedNormals.y, summedNormals.z);
                    // Normalize the sliding direction and make it orthogonal to the hit normal
                    Vector3.OrthoNormalize(ref summedNormals, ref slidingDirection);
                    // Set the movement direction to the combined sliding directions
                    _movementDirection = slidingDirection * slope * 0.2f;
                    Debug.DrawRay(transform.position + Vector3.up * _characterController.GetActualHeight(), slidingDirection * slope * 0.2f, Color.magenta);
                }
            } else {
                ResetSliding();
                // Turn off the anti-stuck mode
                _antiStuckEnabled = false;
            }
        }

        /// <summary>
        /// Resets all internal variables related to the sliding mechanic
        /// </summary>
        protected virtual void ResetSliding() {
            _sliding = false;
            // Reset the buffer
            _slidingBuffer = SlidingTimeout;
        }

        /// <summary>
        /// Checks if the slope given by its normal is too steep to move on
        /// </summary>
        /// <param name="slopeNormal">Normal of the slope to check</param>
        /// <param name="slope">Computed slope degrees which were used for the check</param>
        /// <returns>True if the slope is too steep, otherwise false</returns>
        protected virtual bool SlopeTooSteep(Vector3 slopeNormal, out float slope) {
            slope = Mathf.Round(Vector3.Angle(slopeNormal, Vector3.up));
            return slope > _characterController.slopeLimit;
        }

        /// <summary>
        /// Checks if the character is close to the currently set water level
        /// </summary>
        /// <returns>True if the character is close enough to the considered water level, otherwise false</returns>
        protected virtual bool IsCloseToWaterSurface() {
            return Mathf.Abs(_currentWaterHeight - (transform.position.y + SwimmingStartHeight)) < 0.1f;
        }

        /// <summary>
        /// Gets the current water height/level based on all touched waters
        /// </summary>
        /// <returns>Maximum water height of all touched waters, -Infinity if no water is touched</returns>
        protected virtual float GetCurrentWaterHeight() {
            return _touchedWaters.Max?.GetHeight() ?? -Mathf.Infinity;
        }

        /// <summary>
        /// Prevents the character from swimming above the surface of the water it is currently swimming in
        /// </summary>
        protected virtual void PreventSwimmingAboveSurface() {
            // Check if the planned move in Y direction would lead to the character being above the current water level
            if (_movementDirection.y * Time.deltaTime + _swimmingStartHeight > _currentWaterHeight) {
                // Prevent that the character can swim above the water level => cap the movement in Y direction
                _movementDirection.y = Mathf.Min(0, _movementDirection.y);
            }
        }

        /// <summary>
        /// Returns if the character is grounded
        /// </summary>
        /// <returns>True if grounded, otherwise false</returns>
        public virtual bool IsGrounded() {
            return _grounded;
        }

        /// <summary>
        /// Returns if the character is sliding
        /// </summary>
        /// <returns>True if sliding, otherwise false</returns>
        public virtual bool IsSliding() {
            return _sliding;
        }

        /// <summary>
        /// Returns if the character is sprinting
        /// </summary>
        /// <returns>True if sprinting, otherwise false</returns>
        public bool IsSprinting() {
            return _sprinting;
        }

        /// <summary>
        /// Initializes the internal _colliderBottom variable 
        /// </summary>
        protected virtual void InitColliderBottom() {
            _colliderBottom = new Sphere();
            UpdateColliderBottom();
        }

        /// <summary>
        /// Updates variable value _colliderBottom to its current world coordinates 
        /// </summary>
        protected virtual void UpdateColliderBottom() {
            _colliderBottom.Radius = _characterController.GetActualRadius();
            _colliderBottom.Center = transform.TransformPoint(_characterController.center) + Vector3.down * (_characterController.GetActualHeight() * 0.5f - _colliderBottom.Radius);
        }

        /// <summary>
        /// Moves the character in the given direction
        /// </summary>
        /// <param name="direction">Movement direction</param>
        public virtual void Move(Vector3 direction) {
            if (direction.magnitude == 0) {
                return;
            }

            if (IsInMotion()) {
                if (!_motionEventInvoked) {
                    Motion?.Invoke();
                    _motionEventInvoked = true;
                }
            } else {
                _motionEventInvoked = false;
            }

            /* Disabling this for now
            if (_swimming && !_grounded) {
                // Special case for swimming
                float yDeltaNeeded = direction.y;
                float yBefore = transform.position.y;

                // Move the character
                _characterController.Move(direction);

                float yDeltaResult = transform.position.y - yBefore;
                if (yDeltaResult > yDeltaNeeded) {
                    // Do not allow inaccuracy of the Character Controller's Move method in this case!
                    // Otherwise, it would be possible to swim over the water level
                    Translate(Vector3.down * (yDeltaResult - yDeltaNeeded), true);
                }
            } else {*/
            // Move the character
            _characterController.Move(direction);
            //}

            // Update the collider bottom world coordinates
            UpdateColliderBottom();
        }

        /// <summary>
        /// Checks if the character is allowed to move in the 3rd dimension (up/down)
        /// </summary>
        /// <returns>True if the character is allowed, otherwise false</returns>
        protected virtual bool Allow3dMovement() {
            return _swimming || _flyingBuffer < 0;
        }

        /// <summary>
        /// Checks if the character is allowed to jump
        /// </summary>
        /// <returns>True if the character is allowed, otherwise false</returns>
        protected virtual bool AllowJumping() {
            return _canMove
                    && !_sliding
                    && !_3dMovementEnabled
                    && !IsClimbing();
        }

        /// <summary>
        /// Triggers a jump in the current frame if possible
        /// </summary>
        public virtual void Jump() {
            if (AllowJumping()) {
                if (_grounded) {
                    _jump = true;
                } else if (EnableMidairJumps && _midairJumpsCount < AllowedMidairJumps) {
                    _jump = true;
                    _midairJumpsCount++;
                }
            }
        }

        /// <summary>
        /// Lets the character perform a jump in the current frame
        /// </summary>
        protected virtual void PerformJump() {
            _jump = false;

            if (_crouching) {
                // Jumping cancels crouching
                _crouching = false;
            } else {
                _antiStuckEnabled = false;

                if (HasPlanarMovement()) {
                    _runningJump = true;
                }

                float jumpHeight = CalculateJumpHeight();

                if (EnableMidairJumps
                    && RewardPerfectMidairJump
                    && IsAtPeakJumpHeight()) {
                    GainPerfectJumpReward(ref jumpHeight);

                    if (LogApproxJumpPeakDuration) {
                        Debug.Log("Perfect midair jump performed!");
                    }
                }

                _movementDirection.y = jumpHeight;

                if (_animator) {
                    _animator?.SetTrigger(__jump);
                }

                _currentState = MotorState.Jumping;

                if (EnableMidairJumps
                    && LogApproxJumpPeakDuration
                    && !_loggingJumpPeakDuration) {
                    StartCoroutine(PeakJumpDurationMeasurement());
                }
            }
        }

        /// <summary>
        /// Checks if the character is at its peak jump height. Uses variable JumpPeakTolerance for measurement
        /// </summary>
        /// <returns>True if the character is at its jump peak height, otherwise false</returns>
        public virtual bool IsAtPeakJumpHeight() {
            if (_currentState == MotorState.Jumping || _currentState == MotorState.Falling) {
                if (Mathf.Abs(_characterController.velocity.y) < JumpPeakTolerance) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the reward for performing a perfect midair jump
        /// </summary>
        /// <param name="originalJumpHeight">The previously computed jump height as changable reference</param>
        protected virtual void GainPerfectJumpReward(ref float originalJumpHeight) {
            originalJumpHeight *= PerfectDoubleJumpMultiplier;
        }

        /// <summary>
        /// Coroutine for measuring the peak jump duration of the character
        /// </summary>
        protected virtual IEnumerator PeakJumpDurationMeasurement() {
            _loggingJumpPeakDuration = true;
            float duration = 0;
            while (_currentState == MotorState.Jumping || _currentState == MotorState.Falling) {
                yield return null;
                if (IsAtPeakJumpHeight()) {
                    duration += Time.deltaTime;
                } else if (duration > 0) {
                    break;
                }
            }
            duration += Time.deltaTime;
            Debug.Log("The last jump had an approx. perfect midair jump time window of " + duration + " seconds");
            _loggingJumpPeakDuration = false;
        }

        /// <summary>
        /// Enables/Disables sprinting
        /// </summary>
        /// <param name="on">The new value for the internal sprinting variable</param>
        public virtual void Sprint(bool on) {
            _sprinting = on && (_player?.CanSprint() ?? true);
        }

        /// <summary>
        /// Toggles crouching
        /// </summary>
        /// <param name="toggle">The new value for the internal crouching toggle variable</param>
        public virtual void ToggleCrouching(bool toggle) {
            if (toggle && _grounded && !_3dMovementEnabled) {
                _crouching = !_crouching;
            }
        }

        /// <summary>
        /// Toggles walking
        /// </summary>
        /// <param name="toggle">The new value for the internal walking toggle variable</param>
        public virtual void ToggleWalking(bool toggle) {
            if (toggle && !_swimming) {
                _walking = !_walking;
            }
        }

        /// <summary>
        /// Toggles autorunning
        /// </summary>
        /// <param name="toggle">If null, autorunning is toggled dependent on the current state. If true/false, autorunning is enabled/stopped</param>
        public virtual void ToggleAutorunning(bool? on = null) {
            if (on == null) {
                // Toggle
                _autorunning = !_autorunning;
            } else {
                // Set new value
                _autorunning = on.GetValueOrDefault();
            }
        }

        /// <summary>
        /// Sets the character's direction input by the player/controller
        /// </summary>
        /// <param name="direction">The new input direction</param>
        public virtual void SetInputDirection(Vector3 direction) {
            _inputDirection = direction;
        }

        /// <summary>
        /// Sets the horizontal rotation that should be performed
        /// </summary>
        /// <param name="rotation">Amount to strafe</param>
        public virtual void SetRotation(float rotation) {
            _rotate = rotation;
        }

        /// <summary>
        /// Sets the strafing that should be performed
        /// </summary>
        /// <param name="strafe">Amount to strafe</param>
        public virtual void SetStrafe(float strafe) {
            _strafe = strafe;
        }

        /// <summary>
        /// Sets if the character should align with the camera
        /// </summary>
        /// <param name="on">If true, the character should align</param>
        public virtual void SetAlignWithCamera(bool on) {
            _alignWithCamera = on;
        }

        /// <summary>
        /// Sets if the camera should pause rotation and not rotate in sync with the character rotation
        /// </summary>
        /// <param name="on">If true, rotation is paused (the character rotates alone)</param>
        public virtual void PauseCameraRotation(bool on) {
            _pauseCameraRotation = on;
        }

        /// <summary>
        /// Sets if horizontal character rotation input should be modified, e.g. used as strafing input instead of rotation input
        /// </summary>
        /// <param name="rotationModifier">If true, the rotation input is modified</param>
        public virtual void SetRotationInputModification(bool rotationModifier) {
            _rotationInputModified = rotationModifier;
        }

        /// <summary>
        /// Gets the character's forward direction in the horizontal (xz) plane
        /// </summary>
        /// <returns>Character's forward vector in world coordinates</returns>
        protected virtual Vector3 GetForwardInHorizontalPlane() {
            return Vector3.Cross(transform.right, Vector3.up);
        }

        /// <summary>
        /// Sets if the character should surface
        /// </summary>
        /// <param name="surface">If true, the character should surface</param>
        public virtual void Surface(bool surface) {
            _surface = _3dMovementEnabled && surface && _canMove;
        }

        /// <summary>
        /// Sets if the character should dive
        /// </summary>
        /// <param name="surface">If true, the character should dive</param>
        public virtual void Dive(bool dive) {
            _dive = _3dMovementEnabled && dive && _canMove;
        }

        /// <summary>
        /// Applies all currently enabled movement speed modifiers to the given value
        /// </summary>
        /// <param name="current">Value to be modified</param>
        /// <returns>Current movement speed modified by active modifiers</returns>
        protected virtual float ApplyMovementSpeedMultipliers(float current) {
            float result = current;

            if (_swimming) {
                result *= SwimSpeedMultiplier;
            } else if (_flying) {
                result = 2.0f * current;
            } else if (IsClimbing()) {
                // Allow sprinting while climbing
                if (_sprinting) {
                    result *= SprintSpeedMultiplier;
                }
            } else {
                // Adjust the speed if crouching is enabled
                if (_crouching) {
                    result = Mathf.Min(CrouchSpeed, result);
                    // Disable walking while crouching
                    _walking = false;
                }
                // Multiply with the sprint multiplier if sprinting is active
                if (_sprinting) {
                    result *= SprintSpeedMultiplier;
                }
                // Set the speed if walking is enabled
                if (_walking) {
                    result = Mathf.Min(WalkSpeed, result);
                }
            }

            return result * (_player?.GetMovementSpeedModifier() ?? 1.0f);
        }

        /// <summary>
        /// Teleports the character to the given Vector3
        /// </summary>
        /// <param name="targetPosition">Position to teleport to</param>
        public virtual void TeleportTo(Vector3 targetPosition) {
            if (!Time.inFixedTimeStep) {
                Debug.LogWarning("Method TeleportTo was called outside of FixedUpdate! This can lead to unexpected side effects (double-click here for more information)");
                // Not called from within FixedUpdate => disable the character controller component so that it does not overwrite the target position
                // The disadvantage is that collision detection callbacks are called again (OnTriggerEnter) or never (OnTriggerExit)
                _characterController.enabled = false;
            }

            transform.position = targetPosition;
            UpdateColliderBottom();

            if (!Time.inFixedTimeStep) {
                // Enable the character controller again
                _characterController.enabled = true;
            }
        }

        /// <summary>
        /// Teleports the character to the given Transform
        /// </summary>
        /// <param name="target">Target Transform</param>
        /// <param name="withYRotation">If true, the character's Y axis rotation is additionally aligned with the Y axis rotation of the given Transform</param>
        public virtual void TeleportTo(Transform target, bool withYRotation = false) {
            TeleportTo(target.position);

            if (withYRotation) {
                _facingDirection = target.forward;
                _facingDirection.y = 0;
            }
        }

        /// <summary>
        /// Aligns the object with the camera viewing direction (Y axis rotation only)
        /// </summary>
        /// <returns>The new facing direction</returns>
        public virtual Vector3 AlignWithCameraDirection() {
            Vector3 newFacingDirection = transform.forward;
            Camera usedCamera = _rpgCamera?.GetUsedCamera();
            if (usedCamera) {
                newFacingDirection = Utils.ProjectOnHorizontalPlane(usedCamera.transform.forward);
            }
            TurnInDirection(newFacingDirection);
            return newFacingDirection;
        }

        /// <summary>
        /// Turn the character towards the given position
        /// </summary>
        /// <param name="position">Point to look at</param>
        /// <returns>The new target facing direction</returns>
        public virtual Vector3 TurnTowards(Vector3 position) {
            Vector3 newFacingDirection = Utils.ProjectOnHorizontalPlane(position - transform.position);
            TurnInDirection(newFacingDirection);
            return newFacingDirection;
        }

        /// <summary>
        /// Rotates the character towards a target direction
        /// </summary>
        /// <param name="facingDirection">New facing direction</param>
        public virtual void TurnInDirection(Vector3 facingDirection) {
            _facingDirection = facingDirection;
            _alignmentInProgress = true;
        }

        /// <summary>
        /// Checks if the character is currently in motion
        /// </summary>
        /// <returns>True if the character is in motion, otherwise false</returns>
        public virtual bool IsInMotion() {
            return HasPlanarMovement()
                    || IsFalling()
                    || _surface
                    || _dive;
        }

        /// <summary>
        /// Checks if the character is currently flying, i.e. in midair 
        /// </summary>
        /// <returns>True if the character is flying, otherwise false</returns>
        public virtual bool IsFlying() {
            return (_player?.CanFly() ?? false) && !_grounded;
        }

        /// <summary>
        /// Checks if the character is currently falling, i.e. is neither grounded nor in 3D motion
        /// </summary>
        /// <returns>True if the character is falling, otherwise false</returns>
        public virtual bool IsFalling() {
            return !_grounded && !Is3dMovementEnabled();
        }

        public virtual bool IsClimbing() {
            return _climbingState != ClimbingState.None;
        }

        /// <summary>
        /// Checks if the character is currently strafing
        /// </summary>
        /// <returns>True if the character is strafing, otherwise false</returns>
        protected virtual bool IsStrafing() {
            return _strafe != 0 || IsStrafingViaRotationModifier();
        }

        /// <summary>
        /// Checks if the character is currently strafing via input modification
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsStrafingViaRotationModifier() {
            return _rotate != 0 && (_rotationInputModified || IsLockedOnTarget());
        }

        /// <summary>
        /// Checks if the character is looking in the giving direction
        /// </summary>
        /// <param name="direction">Direction to check</param>
        /// <returns>True if the character is looking in the given direction, otherwise false</returns>
        protected virtual bool IsLookingInDirection(Vector3 direction) {
            return direction == Vector3.zero
                    || Utils.IsAlmostEqual(transform.forward, direction, 2.0f);
        }

        /// <summary>
        /// Determines the current motor state
        /// </summary>
        /// <returns>The state the motor is currently in</returns>
        protected virtual MotorState DetermineCurrentState() {
            if (_currentState == MotorState.Jumping) {
                if (_characterController.velocity.y < 0) {
                    return MotorState.Falling;
                } else if (IsFlying()) {
                    return MotorState.Flying;
                } else {
                    return MotorState.Jumping;
                }
            }

            if (IsSwimming()) {
                return MotorState.Swimming;
            } else if (IsGrounded() && HasPlanarMovement()) {
                if (_walking) {
                    return MotorState.Walking;
                } else if (IsSprinting()) {
                    return MotorState.Sprinting;
                } else if (_crouching) {
                    return MotorState.Crouching;
                } else {
                    return MotorState.Running;
                }
            } else if (IsFlying()) {
                return MotorState.Flying;
            } else if (IsClimbing()) {
                return MotorState.Climbing;
            } else if (IsFalling()) {
                return MotorState.Falling;
            }

            return MotorState.Idle;
        }

        /// <summary>
        /// Gets the current motor state
        /// </summary>
        /// <returns>Current motor state</returns>
        public virtual MotorState GetCurrentState() {
            return _currentState;
        }

        /// <summary>
        /// Gets the current movement vector in world space
        /// </summary>
        /// <returns></returns>
        public virtual Vector3 GetCurrentMovementDirection() {
            return _movementDirection;
        }

        /// <summary>
        /// Gets the current movement speed
        /// </summary>
        /// <returns></returns>
        public virtual float GetCurrentMovementSpeed() {
            return _movementSpeed;
        }

        /// <summary>
        /// Gets the climbing state the character is currently in
        /// </summary>
        /// <returns>Current climbing state</returns>
        public virtual ClimbingState GetCurrentClimbingState() {
            return _climbingState;
        }

        /// <summary>
        /// Checks the player interface info if a lock is active
        /// </summary>
        /// <returns>True if a lock is active, otherwise false</returns>
        protected virtual bool IsLockedOnTarget() {
            return _player?.LockedOnTarget() ?? false;
        }

        /// <summary>
        /// Helper method for returning the player target position
        /// </summary>
        /// <returns>Position of the target in world coordinates, Vector.zero if there is no target</returns>
        protected virtual Vector3 GetTargetPosition() {
            return _player?.GetTargetPosition() ?? Vector3.zero;
        }

        /// <summary>
        /// Sets the run and strafe speed of the motor. Also called by the RPGBuilder integration
        /// </summary>
        /// <param name="newSpeed">New speed values to use for running and strafing</param>
        public virtual void SetMovementSpeed(float newSpeed) {
            RunSpeed = StrafeSpeed = newSpeed;
        }

        /* IRPGMotor interface */

        public virtual bool Is3dMovementEnabled() {
            return _3dMovementEnabled;
        }

        public virtual bool HasPlanarMovement() {
            return _movementDirection.x != 0 || _movementDirection.z != 0;
        }

        public virtual bool IsMovingBackwards() {
            return _inputDirection.z < 0;
        }

        public virtual bool IsSwimming() {
            return _swimming;
        }

        public abstract bool AllowsCameraAlignment();

        public virtual Vector3 RotateVertically(float input, float speed, bool forceCameraRotation = false) {
            Vector3 result = _facingDirection;

            if (!_canRotate
                || IsClimbing()
                || IsLockedOnTarget()
                || input == 0
                || speed == 0) {
                // Do not rotate
                return result;
            }

            float degrees = input * speed;
            // Rotate the character and store the delta rotation
            Quaternion deltaRotation = transform.rotation;
            transform.Rotate(Vector3.up, degrees, Space.World);
            deltaRotation = transform.rotation * Quaternion.Inverse(deltaRotation);

            result = deltaRotation * _facingDirection;

            if (forceCameraRotation
                || AlsoRotateCamera == WithCameraRotation.Always
                || AlsoRotateCamera == WithCameraRotation.PreventOnInput && !_pauseCameraRotation) {
                // Also keep the camera in sync
                _rpgCamera?.Rotate(Axis.X, degrees, true);
            }

            _turningDirection += degrees;

            return result;
        }

        /* ITransportable interface */

        public virtual Transform GetTransform() {
            return transform;
        }

        public virtual float GetColliderRadius() {
            return _characterController.GetActualRadius();
        }

        public virtual bool IsMovingWithMovingGround() {
            return MoveWithMovingGround;
        }

        public virtual bool IsGroundAffectingJumping() {
            return GroundAffectsJumping;
        }

        public virtual void OnExternalMovement(Vector3 translation, Quaternion rotation) {
            _facingDirection = rotation * _facingDirection;
            UpdateColliderBottom();
        }

        /// <summary>
        /// "OnControllerColliderHit is called when the controller hits a collider while performing a Move" - Unity Documentation
        /// </summary>
        public virtual void OnControllerColliderHit(ControllerColliderHit hit) {
            if ((_characterController.collisionFlags & CollisionFlags.Above) != 0
                && _movementDirection.y > 0) {
                _movementDirection.y = 0;
            }

            if (AllowFreeClimbing() && Utils.LayerInLayerMask(hit.gameObject.layer, ClimbableLayers)) {
                // We collided with a climbable collider
                TryFreeClimbing();
            }
        }

        /// <summary>
        /// "OnTriggerEnter happens on the FixedUpdate function when two GameObjects collide" - Unity Documentation
        /// </summary>
        /// <param name="other">Triggering collider</param>
        protected virtual void OnTriggerEnter(Collider other) {
            Water water = other.GetComponent<Water>();
            if (water) {
                // Store the water script for getting the right water level later
                _touchedWaters.Add(water);
            }
        }

        /// <summary>
        /// "OnTriggerExit is called when the Collider other has stopped touching the trigger" - Unity Documentation
        /// </summary>
        /// <param name="other">Left trigger collider</param>
        protected virtual void OnTriggerExit(Collider other) {
            Water water = other.GetComponent<Water>();
            if (water) {
                // Remove the water again since we left it
                _touchedWaters.Remove(water);
            }
        }

        /// <summary>
        /// Draws a sphere cast gizmo
        /// </summary>
        /// <param name="origin">Origin of the sphere cast</param>
        /// <param name="direction">Direction of the cast</param>
        /// <param name="radius">Radius of the cast sphere</param>
        /// <param name="result">The result of the corresponding sphere cast</param>
        protected virtual void DrawSphereCast(Vector3 origin, Vector3 direction, float radius, bool result) {
            Gizmos.color = result ? Color.green : Color.red;
            Gizmos.DrawWireSphere(origin, radius);
            Gizmos.DrawRay(origin + Vector3.forward * radius, direction);
            Gizmos.DrawRay(origin - Vector3.forward * radius, direction);
            Gizmos.DrawRay(origin + Vector3.right * radius, direction);
            Gizmos.DrawRay(origin - Vector3.right * radius, direction);
            Gizmos.DrawRay(origin + Vector3.up * radius, direction);
            Gizmos.DrawRay(origin - Vector3.up * radius, direction);
            Gizmos.DrawWireSphere(origin + direction, radius);
        }

        /// <summary>
        /// If Gizmos are enabled, this method draws some debugging gizmos
        /// </summary>
        protected virtual void OnDrawGizmosSelected() {
            Color green = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color yellow = new Color(1.0f, 1.0f, 0.0f, 0.35f);
            Color red = new Color(1.0f, 0.0f, 0.0f, 0.35f);
            Color blue = new Color(0.0f, 0.0f, 1.0f, 0.55f);
            Color gray = new Color(0.2f, 0.2f, 0.2f, 0.55f);

            if (!_characterController) {
                _characterController = GetComponent<CharacterController>();
            }

            InitColliderBottom();

            if (_sliding) {
                Gizmos.color = yellow;
            } else if (_grounded) {
                Gizmos.color = green;
            } else {
                Gizmos.color = red;
            }

            // Draw sphere for the grounded check area
            Gizmos.DrawSphere(_colliderBottom.Center + Vector3.down * GroundedTolerance, _colliderBottom.Radius);

            // Draw the local Swimming Start Height
            Gizmos.color = blue;
            Gizmos.DrawCube(transform.position + Vector3.up * SwimmingStartHeight, new Vector3(0.7f, 0.01f, 0.7f));

            Gizmos.color = yellow;
            if (EnableMidairJumps
                && DrawGizmoDuringJumpPeak
                && IsAtPeakJumpHeight()) {
                Gizmos.DrawSphere(_colliderBottom.Center + Vector3.up * (_characterController.GetActualHeight() - _characterController.GetActualRadius()), _characterController.GetActualRadius());
            }

            if (EnableLedgeClimbing && DrawLedgeCheckGizmos) {
                // Draw the height to check for grabbable ledges
                Gizmos.color = gray;
                Gizmos.DrawCube(transform.position + Vector3.up * LedgeGrabHeight, new Vector3(0.7f, 0.01f, 0.7f));

                bool ledgeGrabbing = _climbingState == ClimbingState.GrabbingLedge;
                CheckLedgeGrabPossibility(GetReachOutVectorForward(), out RaycastHit ledgeHit, out _, true);
                if (ledgeGrabbing) {
                    AllowClimbingUpLedge(ref ledgeHit, true);
                }
                CheckLedgeGrabPossibility(GetReachOutVectorLeft(), out ledgeHit, out _, ledgeGrabbing);
                CheckLedgeGrabPossibility(GetReachOutVectorRight(), out ledgeHit, out _, ledgeGrabbing);
            }

            if (EnableFreeClimbing && DrawFreeClimbingCheckGizmos) {
                CheckFreeClimbingPossibility(transform.position, out _, out _, true);
            }
        }
    }
}
