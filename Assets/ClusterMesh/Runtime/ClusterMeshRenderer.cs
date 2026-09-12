using UnityEngine;

namespace ClusterMesh
{
    [ExecuteAlways]
    public sealed class ClusterMeshRenderer : MonoBehaviour, ISerializationCallbackReceiver
    {
        public ClusterMeshAsset asset;
        public Camera targetCamera;
        public ComputeShader cullShader;
        public Shader litShader;
        [Tooltip("Write this object into the shadow map (ShadowCaster pass).")]
        public bool castShadows = true;
        [Tooltip("Receive shadows from other objects on this surface.")]
        public bool receiveShadows = true;
        [Tooltip("Cull clusters outside the target camera frustum.")]
        public bool enableCameraCull = true;
        public bool enableConeCull = true;
        [Tooltip("Write this object's transform motion into URP's Motion Vector texture. Disabled by default to avoid history and draw-pass cost.")]
        public bool enableMotionVectors;
        [Tooltip("CPU object cull before dispatch. Off = this object is always submitted.")]
        public bool enableCpuObjectCull = true;
        [InspectorName("探针")]
        [Tooltip("按物体原点插值 Light Probe。没有探针组时，只有开着天光才会退回天空 SH。")]
        public bool enableLightProbes = true;
        [InspectorName("天光")]
        [Tooltip("使用 RenderSettings 天空 / 环境 SH。探针关掉或场景没有探针组时作为间接光。")]
        public bool enableAmbientSky = true;
        [InspectorName("雾")]
        [Tooltip("Forward 混 URP 雾。延迟雾仍由管线按深度做，不受此开关影响。")]
        public bool enableFog = true;
        [Tooltip("Replace lighting with a solid color per cluster.")]
        public bool showClusterColors;
        public bool showClusterAabb;
        [Tooltip("Screen-pixel LOD error. 0 = leaves only.")]
        public float lodErrorThreshold;
        [Tooltip("Draw which LOD each visible cluster uses.")]
        public bool showLodLevels;

        [SerializeField, HideInInspector] int lightingToggleVersion;
        bool _registered;
        bool _hasMotionHistory;
        int _motionHistoryFrame = int.MinValue;
        Matrix4x4 _lastMotionMatrix;
        Matrix4x4 _previousMotionMatrix;

        public Matrix4x4 CapturePreviousMotionMatrix(Matrix4x4 current, int frame)
        {
            if (!enableMotionVectors)
            {
                ResetMotionHistory();
                return current;
            }

            if (!_hasMotionHistory || frame < _motionHistoryFrame)
            {
                _hasMotionHistory = true;
                _motionHistoryFrame = frame;
                _lastMotionMatrix = current;
                _previousMotionMatrix = current;
                return current;
            }

            if (frame != _motionHistoryFrame)
            {
                _previousMotionMatrix = _lastMotionMatrix;
                _motionHistoryFrame = frame;
            }
            _lastMotionMatrix = current;
            return _previousMotionMatrix;
        }

        public void ResetMotionHistory()
        {
            _hasMotionHistory = false;
            _motionHistoryFrame = int.MinValue;
        }

        public void EnsureInitialized()
        {
#if UNITY_EDITOR
            if (cullShader == null)
                cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
#endif
            if (litShader == null)
                litShader = Shader.Find("ClusterMesh/Lit");
            SyncRegistration();
        }

        public void OnBeforeSerialize()
        {
            if (lightingToggleVersion < 1)
                lightingToggleVersion = 1;
        }

        public void OnAfterDeserialize()
        {
            if (lightingToggleVersion >= 1)
                return;
            enableLightProbes = true;
            enableAmbientSky = true;
            enableFog = true;
            lightingToggleVersion = 1;
        }

        void OnEnable()
        {
            EnsureInitialized();
        }

        void OnDisable()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
            ResetMotionHistory();
        }

        void OnValidate()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
            ResetMotionHistory();
            if (isActiveAndEnabled)
                EnsureInitialized();
        }

        void LateUpdate()
        {
            EnsureInitialized();
            ClusterMeshSceneBatcher.Flush();
        }

        void SyncRegistration()
        {
            bool has = asset != null && asset.clusters != null && asset.clusters.Length > 0;
            if (has && isActiveAndEnabled)
            {
                ClusterMeshSceneBatcher.Register(this);
                _registered = true;
            }
            else if (_registered)
            {
                ClusterMeshSceneBatcher.Unregister(this);
                _registered = false;
            }
        }

        void OnDrawGizmos()
        {
            if (!showLodLevels || asset == null || asset.clusters == null)
                return;
            Camera cam = ResolveGizmoCamera();
            if (cam == null)
                return;

            float scale = ClusterMeshLod.ProjectionScale(cam);
            bool perspective = !cam.orthographic;
            Matrix4x4 m = transform.localToWorldMatrix;
            Gizmos.matrix = m;
            for (int i = 0; i < asset.clusters.Length; i++)
            {
                ClusterHeader h = asset.clusters[i];
                if (!ClusterMeshLod.IsClusterVisible(
                        i, asset.clusters, asset.groups, m, cam.transform.position, scale,
                        lodErrorThreshold, asset.hierarchyVersion, perspective))
                    continue;

                Gizmos.color = ClusterMeshLod.LevelColor(ClusterMeshLod.Level(h.flags));
                Gizmos.DrawWireCube(h.aabbCenter, (Vector3)h.aabbExtents * 2f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }

        Camera ResolveGizmoCamera()
        {
            if (targetCamera != null)
                return targetCamera;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                return UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            return Camera.main;
        }

        void OnDrawGizmosSelected()
        {
            if (!showClusterAabb || asset == null || asset.clusters == null)
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < asset.clusters.Length; i++)
            {
                ClusterHeader h = asset.clusters[i];
                Gizmos.color = showClusterColors
                    ? ClusterMeshDebugColors.Rgb((uint)i)
                    : new Color(0.2f, 1f, 0.35f, 1f);
                Gizmos.DrawWireCube(h.aabbCenter, (Vector3)h.aabbExtents * 2f);
            }
        }
    }
}
