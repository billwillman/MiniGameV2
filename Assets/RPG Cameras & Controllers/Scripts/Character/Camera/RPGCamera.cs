using System.Collections;
using System.Collections.Generic;
using JohnStairs.RCC.Character.Cam.Enums;
using JohnStairs.RCC.Character.Motor;
using JohnStairs.RCC.Enums;
using JohnStairs.RCC.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JohnStairs.RCC.Character.Cam {
    [RequireComponent(typeof(RPGViewFrustum))]
    public class RPGCamera : MonoBehaviour, IRPGCamera {
        /// <summary>
        /// Enum value for setting up which camera object should be used by the script, e.g. the main camera of the scene, a newly spawned camera object or the camera assigned to "CameraToUse"
        /// </summary>
        [Tooltip("Enum value for setting up which camera object should be used by the script, e.g. the main camera of the scene, a newly spawned camera object or the camera assigned to \"Camera To Use\".")]
        public CameraSelection CameraToUse = CameraSelection.MainCamera;
        /// <summary>
        /// Defines margins for the viewport in x and y direction
        /// </summary>
        [Tooltip("Defines margins for the viewport in x and y direction.")]
        public Vector2 ViewportMargin = new Vector2(0.2f, 0.2f);
        /// <summary>
        /// Reference to the camera object used by the script. If no camera is assigned to this variable, a new one will be generated automatically when entering the play mode
        /// </summary>
        [Tooltip("Reference to the camera object used by the script. If no camera is assigned to this variable, a new one will be generated automatically when entering the play mode.")]
        public Camera UsedCamera;
        /// <summary>
        /// Behavior the camera should show when following the camera pivot
        /// </summary>
        [Tooltip("Behavior the camera should show when following the camera pivot.")]
        public Follow FollowBehavior;
        /// <summary>
        /// Skybox which is currently used or should be used by the camera object. The skybox can be changed at runtime by calling the script's method "SetUsedSkybox(material)"
        /// </summary>
        [Tooltip("Skybox which is currently used or should be used by the camera object. The skybox can be changed at runtime by calling the script's method \"SetUsedSkybox(material)\".")]
        public Material UsedSkybox;
        /// <summary>
        /// Position of the camera pivot in local character coordinates. Turn on Gizmos to display it as a small cyan sphere
        /// </summary>
        [Tooltip("Position of the camera pivot in local character coordinates. Turn on Gizmos to display it as a small cyan sphere.")]
        public Vector3 CameraPivotLocalPosition = new Vector3(0, 1.5f, 0);
        /// <summary>
        /// Enables the intelligent pivot that moves away from obstacles which the player could see through if zooming in enough (internal pivot only)
        /// </summary>
        [Tooltip("Enables the intelligent pivot that moves away from obstacles which the player could see through if zooming in enough (internal pivot only).")]
        public bool EnableIntelligentPivot = true;
        /// <summary>
        /// The time needed for moving the intelligent pivot away from the obstacle
        /// </summary>
        [Tooltip("The time needed for moving the intelligent pivot away from the obstacle.")]
        public float IntelligentPivotSmoothTime = 0.7f;
        /// <summary>
        /// The time needed to reach the new pivot position while the character is moving
        /// </summary>
        [Tooltip("The time needed to reach the new pivot position while the character is moving.")]
        public float PivotSmoothTime = 0;
        /// <summary>
        /// If set to false, all camera controls are disabled. Can be used to turn off the camera when interacting with a GUI (e.g. see the demo GUI interaction)
        /// </summary>
        [Tooltip("If set to false, all camera controls are disabled. Can be used to turn off the camera when interacting with a GUI (e.g. see the demo GUI interaction).")]
        public bool ActivateControl = true;
        /// <summary>
        /// If set to true, Unity's legacy input system is used
        /// </summary>
        [Tooltip("If set to true, Unity's legacy input system is used.")]
        public bool UseLegacyInputSystem = false;
        /// <summary>
        /// Let the camera always orbit around its pivot without pressing any input
        /// </summary>
        [Tooltip("Let the camera always orbit around its pivot without pressing any input.")]
        public bool AlwaysActivateOrbiting = false;
        /// <summary>
        /// Handles when the cursor should be hidden
        /// </summary>
        [Tooltip("Handles when the cursor should be hidden.")]
        public CursorHiding HideCursor = CursorHiding.WhenOrbiting;
        /// <summary>
        /// How the cursor should behave when orbiting
        /// </summary>
        [Tooltip("How the cursor should behave when orbiting.")]
        public CursorBehavior CursorBehaviorOrbiting = CursorBehavior.Stay;
        /// <summary>
        /// Locks the horizontal orbit axis
        /// </summary>
        [Tooltip("Locks the horizontal orbit axis.")]
        public bool LockRotationX = false;
        /// <summary>
        /// Locks the vertical orbit axis
        /// </summary>
        [Tooltip("Locks the vertical orbit axis.")]
        public bool LockRotationY = false;
        /// <summary>
        /// Inverts the horizontal orbit axis
        /// </summary>
        [Tooltip("Inverts the horizontal orbit axis.")]
        public bool InvertRotationX = true;
        /// <summary>
        /// Inverts the vertical orbit axis
        /// </summary>
        [Tooltip("Inverts the vertical orbit axis.")]
        public bool InvertRotationY = true;
        /// <summary>
        /// The sensitivity of orbiting the camera on the horizontal axis
        /// </summary>
        [Tooltip("The sensitivity of orbiting the camera on the horizontal axis.")]
        public float RotationXSensitivity = 4.0f;
        /// <summary>
        /// The sensitivity of orbiting the camera on the vertical axis
        /// </summary>
        [Tooltip("The sensitivity of orbiting the camera on the vertical axis.")]
        public float RotationYSensitivity = 4.0f;
        /// <summary>
        /// Constrain the camera's orbit horizontal axis from "RotationXMin" degrees to "RotationXMax" degrees
        /// </summary>
        [Tooltip("Constrain the camera's orbit horizontal axis from \"Min Degrees\" degrees to \"Max Degrees\".")]
        public bool ConstrainRotationX = false;
        /// <summary>
        /// The minimum degrees for orbiting on the horizontal axis. Needs "ConstrainRotationX" to be true
        /// </summary>
        [Tooltip("The minimum degrees for orbiting on the horizontal axis. Needs \"Constrain Rotation X\" to be true.")]
        public float RotationXMin = -90.0f;
        /// <summary>
        /// The maximum degrees for orbiting on the horizontal axis. Needs "ConstrainRotationX" to be true
        /// </summary>
        [Tooltip("The maximum degrees for orbiting on the horizontal axis. Needs \"Constrain Rotation X\" to be true.")]
        public float RotationXMax = 90.0f;
        /// <summary>
        /// The minimum degrees for orbiting on the vertical axis
        /// </summary>
        [Tooltip("The minimum degrees for orbiting on the vertical axis.")]
        public float RotationYMin = -89.9f;
        /// <summary>
        /// The maximum degrees for orbiting on the vertical axis
        /// </summary>
        [Tooltip("The maximum degrees for orbiting on the vertical axis.")]
        public float RotationYMax = 89.9f;
        /// <summary>
        /// The time needed for the camera to orbit around its pivot. The higher the smoother the orbiting
        /// </summary>
        [Tooltip("The time needed for the camera to orbit around its pivot. The higher the smoother the orbiting.")]
        public float RotationSmoothTime = 0.1f;
        /// <summary>
        /// The sensitivity of the zooming input
        /// </summary>
        [Tooltip("The sensitivity of the zooming input.")]
        public float ZoomSensitivity = 10.0f;
        /// <summary>
        /// The minimum distance to zoom in to
        /// </summary>
        [Tooltip("The minimum distance to zoom in to.")]
        public float MinDistance = 0;
        /// <summary>
        /// The maximum distance to zoom out to
        /// </summary>
        [Tooltip("The maximum distance to zoom out to.")]
        public float MaxDistance = 20.0f;
        /// <summary>
        /// The time needed to zoom in and out a step
        /// </summary>
        [Tooltip("The time needed to zoom in and out a step.")]
        public float DistanceSmoothTime = 0.4f;
        /// <summary>
        /// The camera's starting degrees on the horizontal axis relative to the character rotation
        /// </summary>
        [Tooltip("The camera's starting degrees on the horizontal axis relative to the character rotation.")]
        public float StartRotationX = 0;
        /// <summary>
        /// If set to true, the start rotation will be relative to the character's start rotation (transform.forward)
        /// </summary>
        [Tooltip("If set to true, the start rotation will be relative to the character's start rotation (transform.forward).")]
        public bool StartRotationRelativeToCharacterRotation = true;
        /// <summary>
        /// The camera's starting degrees on the vertical axis relative to the character rotation
        /// </summary>
        [Tooltip("The camera's starting degrees on the vertical axis relative to the character rotation.")]
        public float StartRotationY = 15.0f;
        /// <summary>
        /// The camera's starting distance
        /// </summary>
        [Tooltip("The camera's starting distance.")]
        public float StartDistance = 7.0f;
        /// <summary>
        /// Enables/disables the initial zoom out from first person view on character spawning
        /// </summary>
        [Tooltip("Enables/disables the initial zoom out from first person view on character spawning.")]
        public bool StartWithZoomOut = false;
        /// <summary>
        /// If set to true, the camera view direction aligns with the character's view/walking direction once the character starts to move (forward/backwards/strafe)
        /// </summary>
        [Tooltip("If set to true, the camera view direction aligns with the character's view/walking direction once the character starts to move (forward/backwards/strafe).")]
        public bool AlignWhenMoving = true;
        /// <summary>
        /// If set to true, rotating the camera pauses camera alignment with the character
        /// </summary>
        [Tooltip("If set to true, rotating the camera pauses camera alignment with the character.")]
        public bool OrbitingPausesCharacterAlignment = true;
        /// <summary>
        /// If set to true, the camera is only aligned with the character if it is locked on a target
        /// </summary>
        [Tooltip("If set to true, the camera is only aligned with the character if it is locked on a target.")]
        public bool AlignOnlyIfLockedOnTarget = false;
        /// <summary>
        /// The time needed to align with the character if "AlignWhenMoving" is set to true
        /// </summary>
        [Tooltip("The time needed to align with the character if \"Align When Moving\" is set to true.")]
        public float AlignmentSmoothTime = 0.2f;
        /// <summary>
        /// Let the camera face the front of the character when walking backwards (requires "AlignWhenMoving")
        /// </summary>
        [Tooltip("Let the camera face the front of the character when walking backwards (requires \"Align When Moving\").")]
        public bool SupportMovingBackwards = false;
        /// <summary>
        /// The number of shakes per second while the camera is shaking
        /// </summary>
        [Tooltip("The number of shakes per second.")]
        public float ShakeFrequency = 5.0f;
        /// <summary>
        /// Maximum rotation change in degrees while the camera is shaking 
        /// </summary>
        [Tooltip("Maximum rotation change in degrees while the camera is shaking.")]
        public float ShakeAmplitude = 10.0f;
        /// <summary>
        /// Maximum amplitude multiplier. E.g. with a value of 2, the amplitude can sometimes be twice as high
        /// </summary>
        [Tooltip("Maximum amplitude multiplier. E.g. with a value of 2, the amplitude can sometimes be twice as high.")]
        public float ShakeAmplitudeVariance = 2.0f;
        /// <summary>
        /// If set to true, the camera will skip the water surface instead of moving continuously through it
        /// </summary>
        [Tooltip("If set to true, the camera will skip the water surface instead of moving continuously through it.")]
        public bool SkipWaterSurface = true;
        /// <summary>
        /// If true, the below effects (fog color and density) are applied if the camera is underwater
        /// </summary>
        [Tooltip("If true, the below effects (fog color and density) are applied if the camera is underwater.")]
        public bool EnableUnderwaterEffect = true;
        /// <summary>
        /// Color of the fog when the camera goes beneath water
        /// </summary>
        [Tooltip("Color of the fog when the camera goes beneath water.")]
        public Color UnderwaterFogColor = new Color(0.48f, 0.76f, 0.67f);
        /// <summary>
        /// Density of the fog which is activated when the camera is under the water surface
        /// </summary>
        [Tooltip("Density of the fog which is activated when the camera is under the water surface.")]
        public float UnderwaterFogDensity = 0.1f;
        /// <summary>
        /// For fine-tune the threshold where the camera is considered as underwater. The higher the value, the earlier the underwater effects kick in
        /// </summary>
        [Tooltip("For fine-tune the threshold where the camera is considered as underwater. The higher the value, the earlier the underwater effects kick in.")]
        public float UnderwaterThresholdTuning = 0;

        /// <summary>
        /// Used view frustum script for camera distance/constraints computations
        /// </summary>
        protected IRPGViewFrustum _rpgViewFrustum;
        /// <summary>
        /// Reference to a RPGMotor script
        /// </summary>
        protected IRPGMotor _rpgMotor;
        /// <summary>
        /// Collider assigned to the character object for computing the pivot retreat for external pivots
        /// </summary>
        protected Collider _collider;
        /// <summary>
        /// Interface for getting character information, e.g. movement impairing effects
        /// </summary>
        protected IPlayer _player;
        /// <summary>
        /// Interface for getting pointer information, e.g. if the pointer is over the GUI
        /// </summary>
        protected IPointerInfo _pointerInfo;
        /// <summary>
        /// Reference to the Unity's PlayerInput component
        /// </summary>
        protected PlayerInput _playerInput;
        /// <summary>
        /// True if the current orbiting was started on a GUI, otherwise false
        /// </summary>
        protected bool _cursorDragStartedOverGUI;
        /// <summary>
        /// Variable for temporarily storing the cursor position for warping
        /// </summary>
        protected Vector2? _tempCursorPosition;
        /// <summary>
        /// Skybox currently used by the used camera
        /// </summary>
        protected Skybox _skybox;
        /// <summary>
        /// If set to true, the UsedSkybox variable has been changed. Used for updating the used camera's skybox
        /// </summary>
        protected bool _skyboxChanged = false;
        /// <summary>
        /// Stored on start: If true, the pivot is internal, i.e. inside the character collider. Controls the activation of needed functionality for each case
        /// </summary>
        protected bool _internalPivot;
        /// <summary>
        /// Current pivot evasive movement direction (intelligent pivot only)
        /// </summary>
        protected Vector3 _pivotMoveDirection;
        /// <summary>
        /// Current pivot smoothing velocity during retreat (intelligent pivot only)
        /// </summary>
        protected Vector3 _pivotRetreatVelocity;
        /// <summary>
        /// Current pivot movement smoothing velocity
        /// </summary>
        protected Vector3 _pivotCurrentVelocity;
        /// <summary>
        /// Current pivot position in world coordinates
        /// </summary>
        protected Vector3 _pivotPosition;
        /// <summary>
        /// For pyramid view frustums, multiple occlusion checks are needed in succession to account for the changing shape (closer distance => less peak stretching)
        /// </summary>
        protected int _maxCheckIterations = 5;
        /// <summary>
        /// Desired camera distance, can be unequal to the current position because of ambient occlusion
        /// </summary>
        protected float _desiredDistance;
        /// <summary>
        /// Current camera distance
        /// </summary>
        protected float _distance = 0;
        /// <summary>
        /// Current camera distance smoothing velocity
        /// </summary>
        protected float _distanceCurrentVelocity;
        /// <summary>
        /// Targeted camera rotation along the X axis
        /// </summary>
        protected float _rotationX = 0;
        /// <summary>
        /// Current camera X rotation smoothed
        /// </summary>
        protected float _rotationXSmooth = 0;
        /// <summary>
        /// Current camera X rotation smoothing velocity
        /// </summary>
        protected float _rotationXVelocity;
        /// <summary>
        /// Targeted camera rotation along the Y axis
        /// </summary>
        protected float _rotationY = 0;
        /// <summary>
        /// Current camera Y rotation smoothed
        /// </summary>
        protected float _rotationYSmooth = 0;
        /// <summary>
        /// Current camera Y rotation smoothing velocity
        /// </summary>
        protected float _rotationYVelocity;
        /// <summary>
        /// The actually used rotation smooth time for smoothing camera rotation/orbiting
        /// </summary>
        protected float _rotationSmoothTime;
        /// <summary>
        /// Each degree above this threshold is added to the _lookUpDegrees
        /// </summary>
        protected float _lookUpThreshold;
        /// <summary>
        /// Degrees the camera is currently looking up
        /// </summary>
        protected float _lookUpDegrees;
        /// <summary>
        /// Smoothed value of _lookUpDegrees
        /// </summary>
        protected float _lookUpDegreesSmooth;
        /// <summary>
        /// Current velocity of the smoothing of _lookUpDegrees
        /// </summary>
        protected float _lookUpRotationVelocity;
        /// <summary>
        /// Currently running shaking coroutine
        /// </summary>
        protected IEnumerator _shakingCoroutine;
        /// <summary>
        /// Delta in rotation X and Y caused by the current shake
        /// </summary>
        protected Vector2 _shakingDelta;
        /// <summary>
        /// Stores all scripts of touched waters, sorted by water height/level
        /// </summary>
        protected SortedSet<Water> _touchedWaters;
        /// <summary>
        /// True if the camera is currently underwater (used for applying/undo the underwater effect)
        /// </summary>
        protected bool _underwater = false;
        /// <summary>
        /// Project setting's fog color at script awakening (used for underwater effect logic)
        /// </summary>
        protected Color _defaultFogColor;
        /// <summary>
        /// Project setting's fog density value at script awakening (used for underwater effect logic)
        /// </summary>
        protected float _defaultFogDensity;
        /// <summary>
        /// True if the menu cursor is enabled
        /// </summary>
        protected bool _menuCursorEnabled;
        #region Input actions
        protected InputAction _activateOrbitingAction;
        protected InputAction _activateOrbitingWithCharacterRotationAction;
        protected InputAction _rotationAmountAction;
        protected InputAction _zoomAction;
        protected InputAction _zoomToMinDistanceAction;
        protected InputAction _zoomToMaxDistanceAction;
        protected InputAction _toggleMenuCursorAction;
        #endregion
        #region Input values
        /// <summary>
        /// When pressed, camera orbiting is allowed/possible
        /// </summary>
        protected bool _inputActivateOrbiting = false;
        /// <summary>
        /// If true, pressing _inputAllowOrbiting started this frame
        /// </summary>
        protected bool _inputActivateOrbitingStart = false;
        /// <summary>
        /// If true, pressing _inputAllowOrbiting stopped this frame
        /// </summary>
        protected bool _inputActivateOrbitingStop = false;
        /// <summary>
        /// Same as above but additionally enables character rotation while orbiting
        /// </summary>
        protected bool _inputActivateOrbitingWithCharRotation = false;
        /// <summary>
        /// If true, pressing _inputAllowOrbitingWithCharRotation started this frame
        /// </summary>
        protected bool _inputActivateOrbitingWithCharRotationStart = false;
        /// <summary>
        /// If true, pressing _inputAllowOrbitingWithCharRotation stopped this frame
        /// </summary>
        protected bool _inputActivateOrbitingWithCharRotationStop = false;
        /// <summary>
        /// Rotation amount along the camera X axis, i.e. around the character's Y axis, as long as orbiting is allowed/triggered
        /// </summary>
        protected Vector2 _inputRotationAmount;
        /// <summary>
        /// Zoom in/out input axis
        /// </summary>
        protected float _inputZoomAmount = 0;
        /// <summary>
        /// Fast zoom into first person view
        /// </summary>
        protected bool _inputMinDistanceZoom = false;
        /// <summary>
        /// Fast zoom out to max character distance
        /// </summary>
        protected bool _inputMaxDistanceZoom = false;
        /// <summary>
        /// If true, the menu cursor is toggled
        /// </summary>
        protected bool _inputToggleMenuCursor = false;
        #endregion Input values

        protected virtual void Awake() {
            _rpgViewFrustum = GetComponent<IRPGViewFrustum>();
            _rpgMotor = GetComponent<IRPGMotor>();
            _collider = GetComponent<Collider>();
            _player = GetComponent<IPlayer>();
            _pointerInfo = GetComponent<IPointerInfo>();
            _playerInput = GetComponent<PlayerInput>();
            _touchedWaters = new SortedSet<Water>(new Water.WaterComparer());
        }

        protected virtual void Start() {
            #region Fog handling
            if (RenderSettings.fog) {
                _defaultFogDensity = RenderSettings.fogDensity;
            } else {
                _defaultFogDensity = 0;
                RenderSettings.fogDensity = 0;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            _defaultFogColor = RenderSettings.fogColor;
            #endregion

            #region Inconsistency handling
            if (AlwaysActivateOrbiting && CursorBehaviorOrbiting == CursorBehavior.Stay) {
                Debug.LogWarning("Cursor Behavior set to \"LockInCenter\" to prevent interference with \"AlwaysActivateOrbiting\"");
                CursorBehaviorOrbiting = CursorBehavior.LockInCenter;
            }

            RPGViewFrustum rpgViewFrustum = GetComponent<RPGViewFrustum>();
            if (rpgViewFrustum && IsLazy()) {
                rpgViewFrustum.EnableCameraLookUp = false;
            }
            #endregion

            UsedCamera = DetermineUsedCamera();

            #region Skybox determination
            if (UsedCamera != null) {
                _skybox = UsedCamera.GetComponent<Skybox>();
                // Check if the used camera has a skybox attached
                if (_skybox == null) {
                    // No skybox attached => add a skybox and assign it to the _skybox variable
                    UsedCamera.gameObject.AddComponent<Skybox>();
                    _skybox = UsedCamera.gameObject.GetComponent<Skybox>();
                }

                if (UsedSkybox) {
                    // Set the used camera's skybox which was set in the inspector
                    _skybox.material = UsedSkybox;
                } else {
                    UsedSkybox = _skybox.material;
                }
            }
            #endregion

            ResetView(StartWithZoomOut);

            InitializeInputActions();
            GetInputs();
            SetUpInputActionCallbacks();

            _pivotPosition = GetDesiredPivotPosition(StartRotationX);
            _internalPivot = HasInternalPivot();
        }

        protected virtual void LateUpdate() {
            UsedCamera = DetermineUsedCamera();
            if (UsedCamera == null) {
                return;
            }

            GetInputs();

            if (_inputToggleMenuCursor) {
                ToggleMenuCursor(!_menuCursorEnabled);
            }

            HandleCursor();

            // Check if the UsedSkybox changed
            if (_skyboxChanged) {
                UpdateSkybox();
            }

            if (EnableUnderwaterEffect) {
                HandleUnderwaterEffects();
            }

            #region Process inputs
            if (ActivateControl) {
                if (!_cursorDragStartedOverGUI
                    && OrbitingActivated()) {

                    #region Rotation X input processing
                    if (!LockRotationX && !IsLazy()) {
                        float rotationXinput = (InvertRotationX ? 1 : -1) * _inputRotationAmount.x;

                        if (_inputActivateOrbitingWithCharRotation
                            && (_player?.CanRotate() ?? true)
                            && !PlayerLockedOnTarget()
                            && !IsPointerOverGUI()) {
                            // Let the character rotate according to the rotation X axis input
                            _rpgMotor?.RotateVertically(rotationXinput, RotationXSensitivity, true);
                        } else {
                            // Allow the camera to orbit
                            _rotationX += rotationXinput * RotationXSensitivity;
                        }

                        if (ConstrainRotationX) {
                            // Clamp the rotation in X axis direction
                            _rotationX = Mathf.Clamp(_rotationX, RotationXMin, RotationXMax);
                        }
                    }
                    #endregion Rotation X input processing

                    #region Rotation Y input processing
                    if (!LockRotationY) {
                        _rotationY += (InvertRotationY ? -1 : 1) * _inputRotationAmount.y * RotationYSensitivity;
                        _rotationY = Mathf.Clamp(_rotationY, RotationYMin, RotationYMax);
                    }
                    #endregion Rotation Y input processing
                }

                _desiredDistance = ComputeDesiredDistance();
            }
            #endregion Process inputs

            #region Look-up logic
            // Check if the camera's Y rotation is contrained by terrain
            bool enableLookUp = !(_rpgMotor?.Is3dMovementEnabled() ?? false)
                                        && !IsLazy()
                                        && !IsShaking()
                                        && UsedCamera.transform.position.y < _pivotPosition.y
                                        && _rpgViewFrustum.IsTouchingGround();
            if (enableLookUp || _lookUpDegreesSmooth < 0) {
                // Continue looking up when we started it but did not finish yet by rotating back to the pivot direction
                _lookUpThreshold = _rotationYSmooth;

                _lookUpDegrees += _rotationY - _lookUpThreshold;
                _lookUpDegrees = Mathf.Max(_lookUpDegrees, RotationYMin - _lookUpThreshold);
                _lookUpDegreesSmooth = Mathf.SmoothDampAngle(_lookUpDegreesSmooth, _lookUpDegrees, ref _lookUpRotationVelocity, RotationSmoothTime);

                if (_lookUpDegreesSmooth < 0) {
                    // Keep the rotation at the threshold
                    _rotationY = _lookUpThreshold;
                } else {
                    // Look-up finished
                    _lookUpDegrees = 0;
                    _lookUpDegreesSmooth = 0;
                }
            }
            #endregion Look-up logic

            SetRotationSmoothTime(RotationSmoothTime);

            #region Camera alignment with the character
            if (AlignWithCharacter()) {
                bool invertAlignment = SupportMovingBackwards && _rpgMotor.IsMovingBackwards();
                if (PlayerLockedOnTarget()) {
                    Quaternion lookRotation = Quaternion.LookRotation(_player.GetTargetPosition() - transform.position, Vector3.up);
                    AlignWithAngle(lookRotation.eulerAngles.y, invertAlignment);
                } else if (!AlignOnlyIfLockedOnTarget) {
                    AlignWithAngle(transform.eulerAngles.y, invertAlignment);
                }

                SetRotationSmoothTime(AlignmentSmoothTime);
            }
            #endregion Camera alignment with the character

            _rotationXSmooth = Mathf.SmoothDamp(_rotationXSmooth, _rotationX, ref _rotationXVelocity, _rotationSmoothTime);
            _rotationYSmooth = Mathf.SmoothDamp(_rotationYSmooth, _rotationY, ref _rotationYVelocity, _rotationSmoothTime);

            // Compute the new camera position            
            UsedCamera.transform.position = ComputeNewCameraPosition();

            // Check if we are in third or first person and adjust the camera rotation behavior
            if (_distance > 0.1f) {
                // In third person => orbit camera
                UsedCamera.transform.LookAt(_pivotPosition);
            } else {
                // In first person => normal camera rotation
                UsedCamera.transform.rotation = Quaternion.Euler(new Vector3(_rotationYSmooth, _rotationXSmooth, 0));
            }
            // Look up
            UsedCamera.transform.Rotate(Vector3.right, _lookUpDegreesSmooth); // "full" rotate due to the LookAt call above

            ConsumeEventInputs();
        }

        /// <summary>
        /// Determines and returns the camera component that should be used controlled by this script. Determination is based on the enum value of CameraToUse
        /// </summary>
        /// <returns>Camera component to be controlled</returns>
        protected virtual Camera DetermineUsedCamera() {
            if (UsedCamera) {
                return UsedCamera;
            } else {
                switch (CameraToUse) {
                    case CameraSelection.AssignedCamera:
                        return UsedCamera;
                    case CameraSelection.MainCamera:
                        return Camera.main;
                    case CameraSelection.SpawnOwnCamera:
                        GameObject camObject = new GameObject(transform.name + transform.GetInstanceID() + " Camera");
                        camObject.AddComponent<Camera>();
                        camObject.AddComponent<FlareLayer>();
                        camObject.AddComponent<Skybox>();
                        return camObject.GetComponent<Camera>();
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Initializes the internal input action variables from the PlayerInput component
        /// </summary>
        public virtual void InitializeInputActions(bool logWarnings = false) {
            if (UseLegacyInputSystem) {
                return;
            }

            if (_playerInput == null) {
                _playerInput = GetComponent<PlayerInput>();
            }
            _activateOrbitingAction = GetInputAction("Activate Orbiting", logWarnings);
            _activateOrbitingWithCharacterRotationAction = GetInputAction("Activate Orbiting With Character Rotation", logWarnings);
            _rotationAmountAction = GetInputAction("Rotation Amount", logWarnings);
            _zoomAction = GetInputAction("Zoom", logWarnings);
            _zoomToMinDistanceAction = GetInputAction("Zoom To Min Distance", logWarnings);
            _zoomToMaxDistanceAction = GetInputAction("Zoom To Max Distance", logWarnings);
            _toggleMenuCursorAction = GetInputAction("Toggle Menu Cursor", logWarnings);
        }

        protected InputAction GetInputAction(string actionName, bool logWarnings = false) {
            try {
                return _playerInput.actions[actionName];
            } catch (KeyNotFoundException) {
                if (logWarnings) {
                    Debug.LogWarning("Input action " + actionName + " not found in " + _playerInput.actions.name);
                }
                return null;
            }
        }

        /// <summary>
        /// Tries to get the input values used by this script
        /// </summary>
        protected virtual void GetInputs() {
            if (!UseLegacyInputSystem) {
                // Poll inputs
                // Orbiting input
                _inputActivateOrbiting = AlwaysActivateOrbiting || _activateOrbitingAction?.ReadValue<float>() > 0;
                // Orbiting with character rotation input
                _inputActivateOrbitingWithCharRotation = _activateOrbitingWithCharacterRotationAction?.ReadValue<float>() > 0;
                // Orbit rotation input
                _inputRotationAmount = _rotationAmountAction?.ReadValue<Vector2>() ?? Vector2.zero;
                // Zoom input
                _inputZoomAmount = _zoomAction?.ReadValue<float>() ?? 0;
            } else {
                // Try to get Unity legacy inputs
                // Orbiting input
                _inputActivateOrbiting = AlwaysActivateOrbiting || Utils.TryGetButton(Utils.InputPhase.Pressed, "Fire1");
                _inputActivateOrbitingStart = Utils.TryGetButton(Utils.InputPhase.Down, "Fire1");
                _inputActivateOrbitingStop = Utils.TryGetButton(Utils.InputPhase.Up, "Fire1");
                // Orbiting with character rotation input
                _inputActivateOrbitingWithCharRotation = Utils.TryGetButton(Utils.InputPhase.Pressed, "Fire2");
                _inputActivateOrbitingWithCharRotationStart = Utils.TryGetButton(Utils.InputPhase.Down, "Fire2");
                // Orbit rotation input
                float inputRotationAmountX = Utils.TryGetAxis(Utils.InputPhase.Smoothed, "Mouse X");
                float inputRotationAmountY = Utils.TryGetAxis(Utils.InputPhase.Smoothed, "Mouse Y");
                _inputRotationAmount = new Vector2(inputRotationAmountX, inputRotationAmountY);
                // Zoom input
                _inputZoomAmount = Utils.TryGetAxis(Utils.InputPhase.Smoothed, "Mouse Scroll Wheel");
                // Input for zooming to min or max distance
                _inputMinDistanceZoom = Utils.TryGetButton(Utils.InputPhase.Pressed, "First Person Zoom");
                _inputMaxDistanceZoom = Utils.TryGetButton(Utils.InputPhase.Pressed, "Maximum Distance Zoom");
                // Cursor-related input
                _inputToggleMenuCursor = Utils.TryGetButton(Utils.InputPhase.Down, "Cancel");
            }
        }

        /// <summary>
        /// Sets up all input action callbacks
        /// </summary>
        protected virtual void SetUpInputActionCallbacks() {
            if (UseLegacyInputSystem) {
                return;
            }
            // Orbiting input
            if (_activateOrbitingAction != null) {
                _activateOrbitingAction.started += context => {
                    _inputActivateOrbitingStart = true;
                    if (!_inputActivateOrbitingWithCharRotation) {
                        _cursorDragStartedOverGUI = ActivateControl && IsPointerOverGUI();
                    }
                };
                _activateOrbitingAction.canceled += context => _inputActivateOrbitingStop = true;
            }

            // Orbiting with character rotation input
            if (_activateOrbitingWithCharacterRotationAction != null) {
                _activateOrbitingWithCharacterRotationAction.started += context => {
                    _inputActivateOrbitingWithCharRotationStart = true;
                    if (!_inputActivateOrbiting) {
                        _cursorDragStartedOverGUI = ActivateControl && IsPointerOverGUI();
                    }
                };
                _activateOrbitingWithCharacterRotationAction.canceled += context => _inputActivateOrbitingWithCharRotationStop = true;
            }

            // Input for zooming to min or max distance
            if (_zoomToMinDistanceAction != null)
                _zoomToMinDistanceAction.started += context => _inputMinDistanceZoom = true;
            if (_zoomToMaxDistanceAction != null)
                _zoomToMaxDistanceAction.started += context => _inputMaxDistanceZoom = true;

            // Cursor-related input
            if (_toggleMenuCursorAction != null)
                _toggleMenuCursorAction.started += context => _inputToggleMenuCursor = true;
        }

        /// <summary>
        /// Resets all variables which are set by input action callbacks
        /// </summary>
        protected virtual void ConsumeEventInputs() {
            if (UseLegacyInputSystem) {
                return;
            }
            _inputActivateOrbitingStart = false;
            _inputActivateOrbitingStop = false;
            _inputActivateOrbitingWithCharRotationStart = false;
            _inputActivateOrbitingWithCharRotationStop = false;
            _inputMinDistanceZoom = false;
            _inputMaxDistanceZoom = false;
            _inputToggleMenuCursor = false;
        }

        /// <summary>
        /// Checks if orbiting is active
        /// </summary>
        /// <returns>True if the camera can orbit, otherwise false</returns>
        protected virtual bool OrbitingActivated() {
            return _inputActivateOrbiting
                    || _inputActivateOrbitingWithCharRotation;
        }

        /// <summary>
        /// Checks if the camera should align with the character
        /// </summary>
        /// <returns>True if the camera should be aligned</returns>
        protected virtual bool AlignWithCharacter() {
            return AlignWhenMoving
                    && _rpgMotor != null
                    && _rpgMotor.AllowsCameraAlignment()
                    && _rpgMotor.HasPlanarMovement()
                    && (!OrbitingPausesCharacterAlignment || !OrbitingActivated());
        }

        /// <summary>
        /// Aligns the camera with the given angle around the Y axis, i.e. horizontally
        /// </summary>
        /// <param name="yAngle">Angle to align with</param>
        /// <param name="inverted">If true, the camera aligns with the angle, but in opposite direction</param>
        protected virtual void AlignWithAngle(float yAngle, bool inverted) {
            Quaternion targetRotation = Quaternion.Euler(new Vector3(0, inverted ? yAngle - 180.0f : yAngle, 0));
            // Compute the delta between the current rotation and the target
            Quaternion delta = targetRotation * Quaternion.Inverse(Quaternion.Euler(new Vector3(0, _rotationX - _shakingDelta.x, 0)));
            float deltaEuler = delta.eulerAngles.y;

            if (Utils.IsAlmostEqual(deltaEuler, 0, 0.5f)) {
                // There is no offset to the camera rotation => no alignment computation required
                return;
            }

            if (deltaEuler > 180.0f) {
                deltaEuler = -(360.0f - deltaEuler);
            }

            _rotationX += deltaEuler;
        }

        /// <summary>
        /// Handles the cursor behavior and visibility
        /// </summary>
        protected virtual void HandleCursor() {
            if (!ActivateControl || _menuCursorEnabled) {
                // No camera-related cursor handling allowed
                return;
            }

            bool orbitingStart = !_cursorDragStartedOverGUI
                                    && (_inputActivateOrbitingStart || _inputActivateOrbitingWithCharRotationStart);
            bool orbitingStop = !_cursorDragStartedOverGUI
                                    && (_inputActivateOrbitingStop && !_inputActivateOrbitingWithCharRotation
                                        || _inputActivateOrbitingWithCharRotationStop && !_inputActivateOrbiting);

            // Handle cursor visibility
            if (HideCursor == CursorHiding.Always) {
                Cursor.visible = false;
            } else if (HideCursor == CursorHiding.WhenOrbiting) {
                if (orbitingStart || AlwaysActivateOrbiting) {
                    Cursor.visible = false;
                } else if (orbitingStop) {
                    Cursor.visible = true;
                }
            } else {
                Cursor.visible = true;
            }

            // Handle cursor behavior while orbiting
            if (CursorBehaviorOrbiting == CursorBehavior.Stay) {
                if (orbitingStart && _tempCursorPosition == null) {
                    // Store mouse position
                    _tempCursorPosition = Mouse.current.position.ReadValue();
                    // Lock the cursor during orbiting
                    Cursor.lockState = CursorLockMode.Locked;
                    // Workaround for a bug in Unity 2019.4
                    // See https://forum.unity.com/threads/mouse-y-position-inverted-in-build-using-mouse-current-warpcursorposition.682627/
#if UNITY_2019_4 && !UNITY_EDITOR
                    Vector2 tempCursorPosition = _tempCursorPosition.GetValueOrDefault();
                    _tempCursorPosition = new Vector2(tempCursorPosition.x, Screen.height - tempCursorPosition.y);
#endif
                } else if (orbitingStop && _tempCursorPosition != null) {
                    // Release the confinement
                    Cursor.lockState = CursorLockMode.None;
                    // Restore mouse position
                    Mouse.current.WarpCursorPosition(_tempCursorPosition.GetValueOrDefault());
                    _tempCursorPosition = null;
                }
            } else if (CursorBehaviorOrbiting == CursorBehavior.LockInCenter) {
                if (orbitingStart || AlwaysActivateOrbiting) {
                    // Lock the cursor
                    Cursor.lockState = CursorLockMode.Locked;
                } else if (orbitingStop) {
                    // Unlock the cursor again
                    Cursor.lockState = CursorLockMode.None;
                }
            } else if (CursorBehaviorOrbiting == CursorBehavior.MoveConfined) {
                Cursor.lockState = CursorLockMode.Confined;
            } else {
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /// <summary>
        /// Toggles the menu cursor based on the given bool
        /// </summary>
        /// <param name="on">If true, the menu cursor will be enabled, otherwise disabled</param>
		public virtual void ToggleMenuCursor(bool on) {
            _menuCursorEnabled = on;

            IRPGController rpgController = GetComponent<IRPGController>();
            if (_menuCursorEnabled) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                _tempCursorPosition = null;
                ActivateControl = false;
                rpgController.DeactivateControls();
            } else {
                ActivateControl = true;
                rpgController.ActivateControls();
            }
        }

        /// <summary>
        /// Computes the desired distance based on the given input
        /// </summary>
        protected virtual float ComputeDesiredDistance() {
            float result = _desiredDistance;

            if (IsPointerOverGUI()) {
                // Do not zoom if the cursor is over the GUI
                return result;
            }

            // Get camera zoom input
            result = Mathf.Clamp(result - _inputZoomAmount * ZoomSensitivity, MinDistance, MaxDistance);

            // Check if one of the switch buttons is pressed
            if (_inputMinDistanceZoom) {
                result = MinDistance;
            } else if (_inputMaxDistanceZoom) {
                result = MaxDistance;
            }

            return result;
        }

        /// <summary>
        /// Gets the camera's start rotation on the horizontal axis, i.e. left/right rotation 
        /// </summary>
        /// <returns>Start rotation on the horizontal axis in degrees</returns>
        protected virtual float GetStartRotationX() {
            return StartRotationX + (StartRotationRelativeToCharacterRotation ? transform.eulerAngles.y : 0);
        }

        /// <summary>
        /// Computes the camera position based on the given parameters as if there were no obstacles
        /// </summary>
        /// <param name="pivotPosition">Position of the camera pivot, i.e. its anchor point</param>
        /// <param name="xAxisDegrees">Orbital rotation degrees around the X axis (up/down)</param>
        /// <param name="yAxisDegrees">Orbital rotation degrees around the Y axis (left/right)</param>
        /// <param name="distance">Distance to the character</param>
        /// <returns>Computed orbital camera position</returns>
        protected virtual Vector3 ComputeCameraPosition(Vector3 pivotPosition, float xAxisDegrees, float yAxisDegrees, float distance) {
            Vector3 offset = -Vector3.forward * distance;
            // Create the combined rotation of X and Y axis rotation
            Quaternion rotXaxis = Quaternion.AngleAxis(xAxisDegrees, Vector3.right);
            Quaternion rotYaxis = Quaternion.AngleAxis(yAxisDegrees, Vector3.up);
            Quaternion rotation = rotYaxis * rotXaxis;

            return pivotPosition + rotation * offset;
        }

        /// <summary>
        /// Computes the camera's pivot position based on the given parameters as if there were no obstacles
        /// </summary>
        /// <param name="yAxisDegrees">Orbital rotation degrees around the Y axis (left/right)</param>
        /// <returns>Computed orbital pivot position</returns>
        protected virtual Vector3 GetDesiredPivotPosition(float yAxisDegrees) {
            Quaternion rotYaxis = Quaternion.AngleAxis(yAxisDegrees, Vector3.up);
            return transform.position + rotYaxis * CameraPivotLocalPosition;
        }

        /// <summary>
        /// Computes the new camera position based on the desired position. Checks for occlusions between the pivot and the camera
        /// and occlusions between the pivot and the "pivot retreat" inside the character collider
        /// </summary>
        /// <returns>Computed orbital position as close as possible to the desired position</returns>
        protected virtual Vector3 ComputeNewCameraPosition() {
            _pivotPosition = Vector3.SmoothDamp(_pivotPosition, ComputeNewPivotPosition(), ref _pivotCurrentVelocity, PivotSmoothTime);

            if (IsLazy() && IsInsideLazyZone()) {
                // Set the rotation X so that the camera stays where it is
                _rotationXSmooth = _rotationX = Utils.SignedAngle(Vector3.forward, GetLookDirection(), Vector3.up);
            }

            _distance = ComputeNewDistance();

            float rotationYSmooth = _rotationYSmooth;
            if (SkipWaterSurface && (_rpgMotor?.IsSwimming() ?? false)) {
                rotationYSmooth = HandleWaterSurfaceSkip(_rotationYSmooth);
            }

            // Compute the new camera position
            return ComputeCameraPosition(_pivotPosition, rotationYSmooth, _rotationXSmooth, _distance);
        }

        /// <summary>
        /// Computes the new pivot position depending on internal or external pivot mechanics
        /// </summary>
        /// <returns>The closest pivot position so that no occlusion occurs</returns>
        protected virtual Vector3 ComputeNewPivotPosition() {
            Vector3 desiredPivotPosition = GetDesiredPivotPosition(_rotationXSmooth);

            // Check if the pivot was set up to be internal or external, i.e. within the character collider or not
            if (_internalPivot) {
                // INTERNAL PIVOT
                if (EnableIntelligentPivot) {
                    return HandleIntelligentPivot(desiredPivotPosition);
                }
                return desiredPivotPosition;
            }

            // EXTERNAL PIVOT
            // Check if there is occlusion between the pivot and the character, i.e. if the pivot should move closer to the character
            Vector3 colliderHead = GetColliderHeadPosition();
            Vector3 pivotRetreat = transform.position;
            pivotRetreat.y = Mathf.Min(desiredPivotPosition.y, colliderHead.y);

            Vector3 viewportExtents = GetViewportExtentsWithMargin();
            float safetyDistance = Mathf.Max(viewportExtents.x, viewportExtents.y, UsedCamera.nearClipPlane);
            Vector3 frustumDirection = desiredPivotPosition - pivotRetreat;
            frustumDirection.Normalize();
            // Add extra length to have the full distance checked (no near clip plane subtracted)
            Vector3 extraLength = frustumDirection * safetyDistance;
            if (_rpgViewFrustum.GetShape() == FrustumShape.Pyramid) {
                extraLength += frustumDirection * UsedCamera.nearClipPlane;
            }

            Vector3 actualTo = desiredPivotPosition + extraLength;
            // Check for occlusion between the pivot retreat and the desired pivot position
            float closestPivotDistance = _rpgViewFrustum.CheckForOcclusion(pivotRetreat, actualTo);
            // Draw the frustum for the pivot [inclusive camera plane]
            _rpgViewFrustum.DrawFrustum(pivotRetreat, actualTo, closestPivotDistance != Mathf.Infinity);

            if (closestPivotDistance == Mathf.Infinity) {
                // Pivot is not occluded => set the pivot position and compute the desired camera position
                return desiredPivotPosition;
            } else {
                // Pivot is occluded => compute the new pivot position
                return pivotRetreat + frustumDirection * (closestPivotDistance - safetyDistance);
            }
        }

        /// <summary>
        /// Handles intelligent pivot logic, i.e. checking the desired pivot position and moving away from obstacles which are too close to it
        /// </summary>
        /// <param name="desiredPivotPosition">Check location</param>
        /// <returns>The new pivot position based on the check result</returns>
        protected virtual Vector3 HandleIntelligentPivot(Vector3 desiredPivotPosition) {
            // Cast rays in all directions according to clip plane height and width
            Vector2 viewportExtents = GetViewportExtentsWithMargin();
            float halfWidth = viewportExtents.x;
            float halfHeight = viewportExtents.y;
            float halfDiagonal = Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

            float horizontal = Mathf.Sqrt(halfWidth * halfWidth + UsedCamera.nearClipPlane * UsedCamera.nearClipPlane);
            float vertical = Mathf.Sqrt(halfHeight * halfHeight + UsedCamera.nearClipPlane * UsedCamera.nearClipPlane);
            float diagonal = Mathf.Sqrt(halfDiagonal * halfDiagonal + UsedCamera.nearClipPlane * UsedCamera.nearClipPlane);

            Vector3[] rayDirections = { Vector3.forward * horizontal, -Vector3.forward * horizontal, Vector3.left * horizontal, Vector3.right * horizontal, Vector3.up * vertical, Vector3.down * vertical,
                                        (Vector3.forward + Vector3.up).normalized * diagonal, (-Vector3.forward + Vector3.up).normalized * diagonal, (Vector3.left + Vector3.up).normalized * diagonal, (Vector3.right + Vector3.up).normalized * diagonal,
                                        (Vector3.forward + Vector3.down).normalized * diagonal, (-Vector3.forward + Vector3.down).normalized * diagonal, (Vector3.left + Vector3.down).normalized * diagonal, (Vector3.right + Vector3.down).normalized * diagonal };

            Vector3 moveDirection = Vector3.zero;
            foreach (Vector3 ray in rayDirections) {
                // Cast the ray
                //Debug.DrawRay(_desiredPivotPosition, ray, Color.magenta);
                if (Physics.Raycast(desiredPivotPosition, ray, out RaycastHit hit, ray.magnitude, _rpgViewFrustum.GetOccludingLayers(), QueryTriggerInteraction.Ignore)) {
                    // Process the hit
                    moveDirection += (hit.point - desiredPivotPosition).normalized * (hit.distance - ray.magnitude);
                }
            }
            // Smooth the resulting evasive movement 
            _pivotMoveDirection = Vector3.SmoothDamp(_pivotMoveDirection, moveDirection, ref _pivotRetreatVelocity, IntelligentPivotSmoothTime);
            // Set the camera pivot position
            return desiredPivotPosition + _pivotMoveDirection;
        }

        /// <summary>
        /// Computes the new camera distance. In case of occlusion, the distance is set to the closest possible position so that the view to the pivot is 
        /// no longer occluded by any obstacle. Otherwise, the camera distance is smoothed to the new distance 
        /// </summary>
        /// <returns>The new current distance</returns>
        protected virtual float ComputeNewDistance() {
            Vector3 desiredPosition = ComputeCameraPosition(_pivotPosition, _rotationYSmooth, _rotationXSmooth, _desiredDistance);
            // Compute the closest possible camera distance by checking if there is something inside the view frustum
            float closestDistance = _rpgViewFrustum.CheckForOcclusion(_pivotPosition, desiredPosition);

            float result;
            if (closestDistance == Mathf.Infinity) {
                // Camera view at the desired position is not contrained => smooth the distance change
                if (IsLazy() && IsInsideLazyZone()) {
                    // Stay at current distance
                    _desiredDistance = GetLookDirection().magnitude;
                    result = _desiredDistance;
                } else {
                    // Normal zoom out behavior
                    result = Mathf.SmoothDamp(_distance, _desiredDistance, ref _distanceCurrentVelocity, DistanceSmoothTime);
                }
                // Fade objects between the pivot and the desired camera position
                _rpgViewFrustum.HandleObjectFading(_pivotPosition, desiredPosition);
            } else {
                // Camera view is constrained => set the camera distance to the closest possible distance 
                Vector3 frustumDirection = desiredPosition - _pivotPosition;
                frustumDirection.Normalize();

                // Compute the closest possible camera position
                Vector3 closestPosition;
                if (_rpgViewFrustum.GetShape() == FrustumShape.Pyramid) {
                    closestDistance = HandlePyramidFrustumOcclusion(closestDistance, frustumDirection, out closestPosition);
                } else {
                    closestDistance = HandleCuboidFrustumOcclusion(closestDistance, frustumDirection, out closestPosition);
                }

                if (_distance < closestDistance) {
                    // Smooth the distance as we move from a smaller constrained distance to a bigger constrained distance
                    result = Mathf.SmoothDamp(_distance, closestDistance, ref _distanceCurrentVelocity, DistanceSmoothTime);
                } else {
                    // Do not smooth if the new closest distance is smaller than the current distance
                    result = closestDistance;
                }
                // Fade objects between the pivot and the closest possible camera position
                _rpgViewFrustum.HandleObjectFading(_pivotPosition, closestPosition);
            }
            // Draw the frustum from pivot to desired camera position inclusive camera plane
            _rpgViewFrustum.DrawFrustum(_pivotPosition, desiredPosition, true);
            return result;
        }

        /// <summary>
        /// Handles the occlusion for the pyramid shaped view frustum
        /// </summary>
        /// <param name="closestDistance">The initially computed closest distance to the target</param>
        /// <param name="frustumDirection">The frustum direction</param>
        /// <param name="closestPosition">The closest possible position to ensure an occlusion-free view</param>
        /// <returns>The closest possible distances without view frustum occlusion</returns>
        protected virtual float HandlePyramidFrustumOcclusion(float closestDistance, Vector3 frustumDirection, out Vector3 closestPosition) {
            float distance = closestDistance;
            _maxCheckIterations = 0;
            do {
                closestDistance = distance;
                // Compute the closest possible camera position
                closestPosition = _pivotPosition + frustumDirection * closestDistance;
                distance = _rpgViewFrustum.CheckForOcclusion(_pivotPosition, closestPosition);
                _maxCheckIterations++;
            } while (distance != Mathf.Infinity && _maxCheckIterations < 5);
            return closestDistance;
        }

        /// <summary>
        /// Handles the occlusion for the cuboid shaped view frustum
        /// </summary>
        /// <param name="closestDistance">The initially computed closest distance to the target</param>
        /// <param name="frustumDirection">The frustum direction</param>
        /// <param name="closestPosition">The closest possible position to ensure an occlusion-free view</param>
        /// <returns>The closest possible distances without view frustum occlusion</returns>
        protected virtual float HandleCuboidFrustumOcclusion(float closestDistance, Vector3 frustumDirection, out Vector3 closestPosition) {
            closestPosition = _pivotPosition + frustumDirection * closestDistance;
            return closestDistance;
        }

        /// <summary>
        /// Checks if the camera has an internal pivot, i.e. a pivot position inside of the used collider 
        /// </summary>
        /// <returns>True if an pivot inside the assigned collider was found, otherwise false</returns>
        public virtual bool HasInternalPivot() {
            // Check assignment since this method can also be called by an Editor script
            if (!_collider) {
                _collider = GetComponent<Collider>();

                // Check again
                if (!_collider) {
                    // No collider component found at all => assume an external pivot
                    return false;
                }
            }
            // Check if within the character collider or not
            return _collider.bounds.Contains(_pivotPosition);
        }

        /// <summary>
        /// Gets the current water height
        /// </summary>
        /// <returns>Current water height if swimming, otherwise -infinity</returns>
        protected virtual float GetCurrentWaterHeight() {
            return _touchedWaters.Max?.GetHeight() ?? -Mathf.Infinity;
        }

        /// <summary>
        /// Gets the distance from the pivot to the water surface on the Y axis
        /// </summary>
        /// <returns>Distance from current pivot to water surface</returns>
        protected virtual float GetPivotDistanceToWaterSurface() {
            return GetCurrentWaterHeight() - _pivotPosition.y;
        }

        /// <summary>
        /// Handles the skipping of the water surface by checking if the camera is inside the dead zone or not and returning
        /// a new rotation Y accordingly
        /// </summary>
        /// <param name="fallback">Value to return if no skip is needed</param>
        /// <returns>Rotation Y at the border of the dead zone if the camera is inside, otherwise the fallback value</returns>
        protected virtual float HandleWaterSurfaceSkip(float fallback) {
            float waterSurfaceAngle = Mathf.Asin(GetPivotDistanceToWaterSurface() / (_distance - UsedCamera.nearClipPlane)) * Mathf.Rad2Deg;
            float shiftedRotationYSmooth = _rotationYSmooth - waterSurfaceAngle;
            float deadZone = Mathf.Atan(GetViewportExtentsWithMargin().y / _distance) * Mathf.Rad2Deg;

            if (Mathf.Abs(shiftedRotationYSmooth) < deadZone) {
                return waterSurfaceAngle + Mathf.Sign(shiftedRotationYSmooth) * deadZone;
            }
            return fallback;
        }

        /// <summary>
        /// Checks if the camera object is underwater
        /// </summary>
        /// <returns>True if the camera object is underwater, otherwise false</returns>
        protected virtual bool IsUnderwater() {
            return UsedCamera.transform.position.y < GetCurrentWaterHeight() + UnderwaterThresholdTuning;
        }

        /// <summary>
        /// Handles turning on/off underwater effects
        /// </summary>
        protected virtual void HandleUnderwaterEffects() {
            // Check if the camera is underwater
            if (IsUnderwater()) {
                // Change the fog settings only once
                if (!_underwater) {
                    _underwater = true;
                    EnableUnderwaterEffects();
                }
            } else {
                // Change the fog settings only once
                if (_underwater) {
                    _underwater = false;
                    DisableUnderwaterEffects();
                }
            }
        }

        /// <summary>
        /// Enables the visual underwater effects
        /// </summary>
        protected virtual void EnableUnderwaterEffects() {
            RenderSettings.fogColor = UnderwaterFogColor;
            RenderSettings.fogDensity = UnderwaterFogDensity;
        }

        /// <summary>
        /// Disables the visual underwater effects
        /// </summary>
        protected virtual void DisableUnderwaterEffects() {
            RenderSettings.fogColor = _defaultFogColor;
            RenderSettings.fogDensity = _defaultFogDensity;
        }

        /// <summary>
        /// Gets the internal variable values defining the current camera position
        /// </summary>
        /// <returns>The internal variable values which define the current camera position in a vector</returns>
        public virtual Vector3 GetPositionParameters() {
            return new Vector3(_rotationXSmooth, _rotationYSmooth, _distance);
        }

        /// <summary>
        /// Gets the position of the collider head in world space. The collider head is expected to be a safe retreat for the external camera pivot
        /// </summary>
        /// <returns>Collider head position in world coordinates, transform.position if no collider is assigned</returns>
        protected virtual Vector3 GetColliderHeadPosition() {
            if (_collider is CharacterController characterController) {
                return transform.TransformPoint(characterController.center) + Vector3.up * (characterController.GetActualHeight() * 0.5f - characterController.GetActualRadius());
            } else if (_collider is CapsuleCollider capsuleCollider) {
                return transform.TransformPoint(capsuleCollider.center) + Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);
            } else if (_collider is BoxCollider boxCollider) {
                return transform.TransformPoint(boxCollider.center);
            } else if (_collider is SphereCollider sphereCollider) {
                return transform.TransformPoint(sphereCollider.center);
            }

            return transform.position;
        }

        /// <summary>
        /// Sets the internal variable values that define the camera position. If smoothTransition is passed as true, the position change will be immediate
        /// </summary>
        /// <param name="rotationX">Camera rotation on the X axis</param>
        /// <param name="rotationY">Camera rotation on the Y axis</param>
        /// <param name="distance">Camera distance</param>
        /// <param name="smoothTransition">If true, the transition to the newly set internal variables will be smoothed over time. Otherwise, the camera is teleported immediately</param>
        public virtual void SetPositionByParameters(float rotationX, float rotationY, float distance, bool smoothTransition = false) {
            _rotationX = rotationX;
            _rotationY = rotationY;
            if (!smoothTransition) {
                _rotationXSmooth = _rotationX;
                _rotationYSmooth = _rotationY;
            }
            _distance = _desiredDistance = distance;
        }

        /// <summary>
        /// Complement to method GetPosition() for setting the internal camera position values
        /// </summary>
        /// <param name="values">Variable values which should be set - see the return vector of GetPosition()</param>
        /// <param name="smoothTransition">If true, the transition to the newly set internal variables will be smoothed over time. Otherwise, the camera is teleported immediately</param>
        public virtual void SetPositionByParameters(Vector3 values, bool smoothTransition = false) {
            SetPositionByParameters(values.x, values.y, values.z, smoothTransition);
        }

        /// <summary>
        /// Moves the camera to the position at newPosition
        /// </summary>
        /// <param name="newPosition">Position the camera should move to</param>
        /// <param name="smoothTransition">If true, the transition to the new position will be smoothed over time. Otherwise, the camera is teleported immediately</param>
        public virtual void MoveTo(Vector3 newPosition, bool smoothTransition = false) {
            Vector3 lookDirection = _pivotPosition - newPosition;
            float distance = lookDirection.magnitude;

            float rotationX = Utils.SignedAngle(Vector3.forward, lookDirection, Vector3.up);
            float rotationY = Vector3.Angle(Vector3.up, lookDirection) - 90.0f;

            SetPositionByParameters(rotationX, rotationY, distance, smoothTransition);
        }

        /// <summary>
        /// Resets the camera view behind the character + starting X rotation, starting Y rotation and starting distance StartDistance
        /// </summary>
        /// <param name="smoothTransition">If true, the transition to the default variable values will be smoothed over time. Otherwise, the camera is teleported immediately</param>        
        public virtual void ResetView(bool smoothTransition = true) {
            _rotationX = GetStartRotationX();
            _rotationY = StartRotationY;
            _desiredDistance = StartDistance;

            if (!smoothTransition) {
                _rotationXSmooth = _rotationX;
                _rotationYSmooth = _rotationY;
                _distance = _desiredDistance;
            }
        }

        /// <summary>
        /// Updates the skybox of the camera UsedCamera
        /// </summary>
        /// <param name="skybox">The new skybox to use</param>
        public virtual void SetUsedSkybox(Material skybox) {
            // Set the new skybox
            UsedSkybox = skybox;
            // Signal that the skybox changed for the next frame
            _skyboxChanged = true;
        }

        /// <summary>
        /// Changes the skybox of the used camera to UsedSkybox
        /// </summary>
        protected virtual void UpdateSkybox() {
            _skybox.material = UsedSkybox;
            _skyboxChanged = false;
        }

        /// <summary>
        /// Sets the rotation smooth time if the camera is not shaking. In this case, the shaking coroutine handles the smooth time
        /// </summary>
        /// <param name="smoothTime">The new smooth time to be set</param>
        protected virtual void SetRotationSmoothTime(float smoothTime) {
            if (!IsShaking()) {
                // Rotation smooth time is handled by the shaking coroutine
                _rotationSmoothTime = smoothTime;
            }
        }

        /// <summary>
        /// Checks if the camera behaves lazily
        /// </summary>
        /// <returns>True if lazy follow behavior is active, otherwise false</returns>
        protected virtual bool IsLazy() {
            return FollowBehavior == Follow.Lazy && !PlayerLockedOnTarget();
        }

        /// <summary>
        /// Checks if the camera is inside the lazy zone, i.e. the zone where lazy follow logic should apply
        /// </summary>
        /// <returns>True if the camera is inside the lazy zone, otherwise false</returns>
        protected virtual bool IsInsideLazyZone() {
            float distanceOnHorizontalPlane = Vector3.ProjectOnPlane(UsedCamera.transform.position - _pivotPosition, Vector3.up).magnitude;
            return MinDistance < distanceOnHorizontalPlane && distanceOnHorizontalPlane < MaxDistance;
        }

        /// <summary>
        /// Gets the look direction from the camera to the camera pivot
        /// </summary>
        /// <returns>Vector from camera to pivot</returns>
        protected virtual Vector3 GetLookDirection() {
            return _pivotPosition - UsedCamera.transform.position;
        }

        /// <summary>
        /// Checks if the camera is currently shaking
        /// </summary>
        /// <returns>True if shaking, otherwise false</returns>
        public virtual bool IsShaking() {
            return _shakingCoroutine != null;
        }

        /// <summary>
        /// Starts camera shaking with the given parameters
        /// </summary>
        /// <param name="frequency">Number of shakes per second while the camera is shaking</param>
        /// <param name="amplitude">Maximum rotation change in degrees while the camera is shaking</param>
        /// <param name="variance">Maximum amplitude multiplier. E.g. with a value of 2, the amplitude can sometimes be twice as high</param>
        public virtual void Shake(float frequency, float amplitude, float variance) {
            if (IsShaking()) {
                StopShaking();
            }

            _shakingCoroutine = ShakingCoroutine(frequency, amplitude, variance);
            StartCoroutine(_shakingCoroutine);
        }

        /// <summary>
        /// Stops camera shaking immediately
        /// </summary>
        public virtual void StopShaking() {
            if (IsShaking()) {
                StopCoroutine(_shakingCoroutine);
                _shakingCoroutine = null;
                _shakingDelta = Vector2.zero;
            }
        }

        /// <summary>
        /// Coroutine for performing the camera shaking effect
        /// </summary>
        protected virtual IEnumerator ShakingCoroutine(float frequency, float amplitude, float variance) {
            float f = 1.0f / frequency;
            _rotationSmoothTime = f;
            Vector2 lastDelta;
            while (true) {
                lastDelta = _shakingDelta;
                _shakingDelta = amplitude * Random.Range(1.0f / variance, variance) * Random.insideUnitCircle.normalized;

                if (Vector2.Angle(lastDelta, _shakingDelta) < 90.0f) {
                    // Make it more extreme
                    _shakingDelta *= -1.0f;
                }
                _rotationX += _shakingDelta.x;
                _rotationY += _shakingDelta.y;
                yield return new WaitForSeconds(f * Random.Range(0.5f, 0.7f));
                _rotationX -= _shakingDelta.x;
                _rotationY -= _shakingDelta.y;
            }
        }

        /// <summary>
		/// Checks if the pointer is over a GUI element
		/// </summary>
		/// <returns>True if the pointer is over a GUI element, otherwise false</returns>
        protected virtual bool IsPointerOverGUI() {
            return _pointerInfo?.IsPointerOverGUI() ?? false;
        }

        /// <summary>
        /// Checks the player interface info if a lock is active
        /// </summary>
        /// <returns>True if a lock is active, otherwise false</returns>
        protected virtual bool PlayerLockedOnTarget() {
            return _player?.LockedOnTarget() ?? false;
        }

        /* IRPGCamera interface */

        public virtual Camera GetUsedCamera() {
            return UsedCamera;
        }

        public virtual Vector2 GetViewportExtentsWithMargin() {
            Vector2 result;
            float halfFieldOfView = UsedCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            result.y = UsedCamera.nearClipPlane * Mathf.Tan(halfFieldOfView);
            result.x = result.y * UsedCamera.aspect + ViewportMargin.x;
            // Now add the viewport margin
            result.y += ViewportMargin.y;
            return result;
        }

        public virtual void Rotate(Axis axis, float degrees, bool immediately) {
            if (axis == Axis.X) {
                _rotationX += degrees;
                if (immediately) {
                    _rotationXSmooth += degrees;
                }
            } else if (axis == Axis.Y) {
                _rotationY += degrees;
                if (immediately) {
                    _rotationYSmooth += degrees;
                }
            }
        }

        public virtual bool IsOrbitingWithCharacterRotation() {
            return _inputActivateOrbitingWithCharRotation;
        }

        /// <summary>
        /// "OnTriggerEnter happens on the FixedUpdate function when two GameObjects collide" - Unity Documentation
        /// </summary>
        /// <param name="other">Collider that entered the trigger collider</param>
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
        /// If Gizmos are enabled, this method draws some utility/debugging spheres
        /// </summary>
        protected virtual void OnDrawGizmos() {
            Color cyan = new Color(0.0f, 1.0f, 1.0f, 0.65f);

            if (Application.isPlaying) {
                // Draw the camera pivot at its position in cyan
                Gizmos.color = cyan;
                Gizmos.DrawSphere(_pivotPosition, 0.1f);
            } else {
                // Draw the camera pivot at its position in cyan
                Gizmos.color = cyan;
                Gizmos.DrawSphere(transform.TransformPoint(CameraPivotLocalPosition), 0.1f);

                // Draw the currently set up start position of the CameraToUse in yellow
                Gizmos.color = Color.yellow;
                _pivotPosition = transform.TransformPoint(CameraPivotLocalPosition);
                Vector3 cameraStartPosition = ComputeCameraPosition(_pivotPosition, StartRotationY, GetStartRotationX(), StartDistance);
                Gizmos.DrawSphere(cameraStartPosition, 0.3f);
            }
        }
    }
}
