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

        ClusterMeshDrawContext _context;
        bool _loggedError;

        public void EnsureInitialized()
        {
            if (_context != null)
                return;
#if UNITY_EDITOR
            if (cullShader == null)
                cullShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ClusterMesh/Shaders/ClusterMeshCull.compute");
            if (litShader == null)
                litShader = Shader.Find("ClusterMesh/Lit");
#endif
            _context = new ClusterMeshDrawContext(asset, cullShader, litShader);
            if (!_context.IsReady && !_loggedError && !string.IsNullOrEmpty(_context.Error))
            {
                Debug.LogError("ClusterMesh: " + _context.Error, this);
                _loggedError = true;
            }
        }

        void OnEnable()
        {
            EnsureInitialized();
        }

        void OnDisable()
        {
            _context?.Dispose();
            _context = null;
        }

        void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            _context?.Dispose();
            _context = null;
            _loggedError = false;
            if (Application.isPlaying)
                EnsureInitialized();
        }

        void LateUpdate()
        {
            EnsureInitialized();
            Camera camera = targetCamera != null ? targetCamera : Camera.main;
            _context?.Draw(transform.localToWorldMatrix, camera);
        }
    }
}
