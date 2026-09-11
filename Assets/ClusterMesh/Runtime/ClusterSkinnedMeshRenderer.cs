using UnityEngine;

namespace ClusterMesh
{
    /// <summary>GPU vertex-texture-fetch renderer for a baked skeletal ClusterMesh asset.</summary>
    [ExecuteAlways]
    public sealed class ClusterSkinnedMeshRenderer : MonoBehaviour
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
        public bool enableCpuObjectCull = true;
        [Tooltip("Replace lighting with a solid color per cluster.")]
        public bool showClusterColors;
        [Tooltip("Draw the current animated cluster AABBs while this object is selected.")]
        public bool showClusterAabb;
        [Tooltip("Screen-pixel error for the baked skinning-aware hierarchy. 0 draws leaf clusters only.")]
        public float lodErrorThreshold;
        [Tooltip("Draw the current animated LOD level and AABB for each selected cluster.")]
        public bool showLodLevels;
        [Tooltip("GPU Texture is the default and performs no per-frame CPU curve evaluation. CPU Curves keeps the compact curve path and is used as an automatic fallback for old assets or unsupported texture formats.")]
        public ClusterSkinnedAnimationEvaluation animationEvaluation = ClusterSkinnedAnimationEvaluation.GpuTexture;
        [InspectorName("前缀并行开启")]
        [Tooltip("仅 CPU Curves 生效。先并行求每根骨头的局部 pose，再用前缀积算骨骼层级。骨架很深或 CPU 实例很少时才可能更快；普通人模请保持关闭，额外 Job 和缓冲往往更贵。")]
        public bool enableParallelBonePrefix;
        public int clipIndex;
        [Range(0f, 1f)] public float normalizedTime;
        public float speed = 1f;
        public bool playAutomatically = true;
        public bool playInEditMode = true;

        bool _registered;

        public void EnsureInitialized()
        {
#if UNITY_EDITOR
            if (cullShader == null)
                cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterSkinnedMeshCull.compute");
#endif
            if (litShader == null)
                litShader = Shader.Find("ClusterMesh/SkinnedLit");
            SyncRegistration();
        }

        void OnEnable() => EnsureInitialized();

        void OnDisable()
        {
            ClusterSkinnedMeshSceneBatcher.Unregister(this);
            _registered = false;
        }

        void OnValidate()
        {
            ClusterSkinnedMeshSceneBatcher.Unregister(this);
            _registered = false;
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
