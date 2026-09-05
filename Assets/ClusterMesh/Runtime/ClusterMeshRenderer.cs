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
        [Tooltip("Write this object into the shadow map (ShadowCaster pass).")]
        public bool castShadows = true;
        [Tooltip("Receive shadows from other objects on this surface.")]
        public bool receiveShadows = true;
        public bool enableConeCull = true;
        [Tooltip("Replace lighting with a solid color per cluster.")]
        public bool showClusterColors;
        public bool showClusterAabb;
        [Tooltip("Screen-pixel LOD error. 0 = leaves only.")]
        public float lodErrorThreshold;
        [Tooltip("Draw which LOD each visible cluster uses.")]
        public bool showLodLevels;

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
