using UnityEngine;

namespace ClusterMesh
{
    [ExecuteAlways]
    public sealed class ClusterMeshRenderer : MonoBehaviour
    {
        public ClusterMeshAsset asset;
        public Camera targetCamera;
        public ComputeShader cullShader;
        public Shader litShader;
        public bool castShadows = true;
        public bool enableConeCull = true;
        [Tooltip("Replace lighting with a solid color per cluster.")]
        public bool showClusterColors;
        public bool showClusterAabb;

        bool _registered;

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

        void OnEnable()
        {
            EnsureInitialized();
        }

        void OnDisable()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
        }

        void OnValidate()
        {
            ClusterMeshSceneBatcher.Unregister(this);
            _registered = false;
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
