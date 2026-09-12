using UnityEngine;

namespace ClusterMesh
{
    /// <summary>GPU vertex-texture-fetch renderer for a baked skeletal ClusterMesh asset.</summary>
    [ExecuteAlways]
    public sealed class ClusterSkinnedMeshRenderer : MonoBehaviour, ISerializationCallbackReceiver
    {
        public ClusterSkinnedMeshAsset asset;
        public Camera targetCamera;
        public ComputeShader cullShader;
        public Shader litShader;
        public bool castShadows = true;
        public bool receiveShadows = true;
        public bool enableCameraCull = true;
        [Tooltip("Only used when the baked animation cone for this cluster is conservative.")]
        public bool enableConeCull = true;
        [Tooltip("Write transform and baked skeletal deformation into URP's Motion Vector texture. Disabled by default to avoid previous-pose evaluation and draw-pass cost.")]
        public bool enableMotionVectors;
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
        [Tooltip("Draw the current animated cluster AABBs while this object is selected.")]
        public bool showClusterAabb;
        [Tooltip("Screen-pixel error for the baked skinning-aware hierarchy. 0 draws leaf clusters only.")]
        public float lodErrorThreshold;
        [Tooltip("Draw the current animated LOD level and AABB for each selected cluster.")]
        public bool showLodLevels;
        [Tooltip("GPU + CPU assets may choose a preferred path. GPU Only and CPU Only assets lock this value to their baked representation.")]
        public ClusterSkinnedAnimationEvaluation animationEvaluation = ClusterSkinnedAnimationEvaluation.GpuTexture;
        [InspectorName("前缀并行开启")]
        [Tooltip("仅 CPU Curves 生效。先并行求每根骨头的局部 pose，再用前缀积算骨骼层级。骨架很深或 CPU 实例很少时才可能更快；普通人模请保持关闭，额外 Job 和缓冲往往更贵。")]
        public bool enableParallelBonePrefix;
        public int clipIndex;
        [Range(0f, 1f)] public float normalizedTime;
        public float speed = 1f;
        public bool playAutomatically = true;
        public bool playInEditMode = true;

        [SerializeField, HideInInspector] int lightingToggleVersion;
        bool _registered;
        bool _hasMotionHistory;
        int _motionHistoryFrame = int.MinValue;
        int _motionHistoryClip = -1;
        Matrix4x4 _lastMotionMatrix;
        Matrix4x4 _previousMotionMatrix;
        float _lastMotionTime;
        float _previousMotionTime;

        public void CapturePreviousMotion(
            Matrix4x4 currentMatrix,
            float currentTime,
            int currentClip,
            int frame,
            out Matrix4x4 previousMatrix,
            out float previousTime)
        {
            if (!enableMotionVectors)
            {
                ResetMotionHistory();
                previousMatrix = currentMatrix;
                previousTime = currentTime;
                return;
            }

            if (!_hasMotionHistory || currentClip != _motionHistoryClip || frame < _motionHistoryFrame)
            {
                _hasMotionHistory = true;
                _motionHistoryFrame = frame;
                _motionHistoryClip = currentClip;
                _lastMotionMatrix = currentMatrix;
                _previousMotionMatrix = currentMatrix;
                _lastMotionTime = currentTime;
                _previousMotionTime = currentTime;
            }
            else
            {
                if (frame != _motionHistoryFrame)
                {
                    _previousMotionMatrix = _lastMotionMatrix;
                    _previousMotionTime = _lastMotionTime;
                    _motionHistoryFrame = frame;
                }
                _lastMotionMatrix = currentMatrix;
                _lastMotionTime = currentTime;
            }

            previousMatrix = _previousMotionMatrix;
            previousTime = _previousMotionTime;
        }

        public void ResetMotionHistory()
        {
            _hasMotionHistory = false;
            _motionHistoryFrame = int.MinValue;
            _motionHistoryClip = -1;
        }

        public void EnsureInitialized()
        {
#if UNITY_EDITOR
            if (cullShader == null)
                cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
#endif
            if (litShader == null)
                litShader = Shader.Find("ClusterMesh/SkinnedLit");
            ConstrainAnimationEvaluation();
            SyncRegistration();
        }

        public void ConstrainAnimationEvaluation()
        {
            if (asset == null)
                return;
            if (asset.animationDataMode == ClusterSkinnedAnimationDataMode.GpuOnly)
                animationEvaluation = ClusterSkinnedAnimationEvaluation.GpuTexture;
            else if (asset.animationDataMode == ClusterSkinnedAnimationDataMode.CpuOnly)
                animationEvaluation = ClusterSkinnedAnimationEvaluation.CpuCurves;
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

        void OnEnable() => EnsureInitialized();

        void OnDisable()
        {
            ClusterSkinnedMeshSceneBatcher.Unregister(this);
            _registered = false;
            ResetMotionHistory();
        }

        void OnValidate()
        {
            ClusterSkinnedMeshSceneBatcher.Unregister(this);
            _registered = false;
            ResetMotionHistory();
            if (isActiveAndEnabled)
                EnsureInitialized();
        }

        void LateUpdate()
        {
            EnsureInitialized();
            ClusterSkinnedMeshSceneBatcher.Flush();
        }

        public float CurrentNormalizedTime(float clock)
        {
            if (!playAutomatically || (!Application.isPlaying && !playInEditMode) || asset == null || asset.clips == null || asset.clips.Length == 0)
                return Mathf.Repeat(normalizedTime, 1f);
            int i = Mathf.Clamp(clipIndex, 0, asset.clips.Length - 1);
            float duration = Mathf.Max(0.0001f, asset.clips[i].duration);
            return Mathf.Repeat(normalizedTime + clock * speed / duration, 1f);
        }

        void SyncRegistration()
        {
            bool has = asset != null && asset.geometry != null && asset.geometry.clusters != null && asset.geometry.clusters.Length > 0;
            if (has && isActiveAndEnabled)
            {
                ClusterSkinnedMeshSceneBatcher.Register(this);
                _registered = true;
            }
            else if (_registered)
            {
                ClusterSkinnedMeshSceneBatcher.Unregister(this);
                _registered = false;
            }
        }
    }
}
