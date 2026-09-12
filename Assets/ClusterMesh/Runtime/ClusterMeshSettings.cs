using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshSettings : ScriptableObject
    {
        public const string ResourceName = "ClusterMeshSettings";
        public const string AssetPath = "Assets/ClusterMesh/Resources/ClusterMeshSettings.asset";

        [SerializeField]
        ClusterMeshMotionVectorSlot motionVectorSlot = ClusterMeshMotionVectorSlot.AfterSkyboxPlus1;

        [Header("Page Streaming")]
        [SerializeField] bool enableGlobalSharedGpuPool = true;
        [SerializeField, Min(8)] int sharedGpuPoolPageCapacity = 256;
        [SerializeField, Range(1, 32)] int maxConcurrentPageReads = 8;
        [SerializeField, Range(1, 16)] int maxPageUploadsPerUpdate = 4;
        [SerializeField, Min(1)] int maxReadMegabytesPerUpdate = 8;
        [SerializeField, Min(1)] int maxUploadMegabytesPerUpdate = 8;
        [SerializeField, Min(4)] int maxDecodedMegabytes = 32;
        [SerializeField] bool enableStreamingPrefetch = true;
        [SerializeField, Range(0, 120)] int streamingResidentGraceUpdates = 12;

        public ClusterMeshMotionVectorSlot MotionVectorSlot
        {
            get => motionVectorSlot;
            set => motionVectorSlot = value;
        }

        public bool EnableGlobalSharedGpuPool { get => enableGlobalSharedGpuPool; set => enableGlobalSharedGpuPool = value; }
        public int SharedGpuPoolPageCapacity { get => Mathf.Clamp(sharedGpuPoolPageCapacity, 8, 4096); set => sharedGpuPoolPageCapacity = Mathf.Clamp(value, 8, 4096); }
        public int MaxConcurrentPageReads { get => Mathf.Clamp(maxConcurrentPageReads, 1, 32); set => maxConcurrentPageReads = Mathf.Clamp(value, 1, 32); }
        public int MaxPageUploadsPerUpdate { get => Mathf.Clamp(maxPageUploadsPerUpdate, 1, 16); set => maxPageUploadsPerUpdate = Mathf.Clamp(value, 1, 16); }
        public int MaxReadMegabytesPerUpdate { get => Mathf.Clamp(maxReadMegabytesPerUpdate, 1, 256); set => maxReadMegabytesPerUpdate = Mathf.Clamp(value, 1, 256); }
        public int MaxUploadMegabytesPerUpdate { get => Mathf.Clamp(maxUploadMegabytesPerUpdate, 1, 256); set => maxUploadMegabytesPerUpdate = Mathf.Clamp(value, 1, 256); }
        public int MaxDecodedMegabytes { get => Mathf.Clamp(maxDecodedMegabytes, 4, 1024); set => maxDecodedMegabytes = Mathf.Clamp(value, 4, 1024); }
        public bool EnableStreamingPrefetch { get => enableStreamingPrefetch; set => enableStreamingPrefetch = value; }
        public int StreamingResidentGraceUpdates { get => Mathf.Clamp(streamingResidentGraceUpdates, 0, 120); set => streamingResidentGraceUpdates = Mathf.Clamp(value, 0, 120); }

        public static ClusterMeshSettings OverrideForTests { get; set; }

        static ClusterMeshSettings _cached;

        public static ClusterMeshMotionVectorSlot CurrentMotionVectorSlot
        {
            get
            {
                if (OverrideForTests != null)
                    return OverrideForTests.MotionVectorSlot;
                ClusterMeshSettings loaded = Loaded;
                return loaded != null ? loaded.MotionVectorSlot : ClusterMeshMotionVectorSlot.AfterSkyboxPlus1;
            }
        }

        public static ClusterMeshSettings Loaded
        {
            get
            {
                if (_cached != null)
                    return _cached;
                _cached = Resources.Load<ClusterMeshSettings>(ResourceName);
                return _cached;
            }
        }

        public static void ClearCacheForTests()
        {
            _cached = null;
        }
    }
}
