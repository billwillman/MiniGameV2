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
        [SerializeField, Min(8), Tooltip("每个兼容池的容量基准，与烘焙必需容量取最大后按 2 的幂对齐；不会因细节排队自动扩容。不是全世界总显存上限。重新加载资产后生效。")]
        int sharedGpuPoolPageCapacity = 256;
        [SerializeField, Tooltip("在既有 GPU 页池内紧凑排列常驻根页；不改模型和流式文件。重新加载资产后生效。")]
        bool packRootPages = true;
        [SerializeField, Range(1, 32)] int maxConcurrentPageReads = 8;
        [SerializeField, Range(1, 64), Tooltip("相邻根页共用一次 FileStream 读取。1 关闭合批；仍受原读取字节、解码内存和上传预算限制。")]
        int maxRootPagesPerRead = 16;
        [SerializeField, Range(16, 1024)] int maxRootReadKiB = 256;
        [SerializeField, Range(1, 64), Tooltip("同一紧凑物理槽中已解码、连续的静态根页合成一次上传。1 关闭；不等待凑批，不扩缓存，仍受字节/时间预算限制。")]
        int maxRootPagesPerUpload = 16;
        [SerializeField, Range(1, 16), Tooltip("每次中央更新最多上传的批次数；细节批次为一页，静态根页可同槽合批。字段名保留以兼容已有配置。")]
        int maxPageUploadsPerUpdate = 4;
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
        public bool PackRootPages { get => packRootPages; set => packRootPages = value; }
        public int SharedGpuPoolPageCapacity { get => Mathf.Clamp(sharedGpuPoolPageCapacity, 8, 4096); set => sharedGpuPoolPageCapacity = Mathf.Clamp(value, 8, 4096); }
        public int MaxConcurrentPageReads { get => Mathf.Clamp(maxConcurrentPageReads, 1, 32); set => maxConcurrentPageReads = Mathf.Clamp(value, 1, 32); }
        public int MaxRootPagesPerRead { get => Mathf.Clamp(maxRootPagesPerRead, 1, 64); set => maxRootPagesPerRead = Mathf.Clamp(value, 1, 64); }
        public int MaxRootReadKiB { get => Mathf.Clamp(maxRootReadKiB, 16, 1024); set => maxRootReadKiB = Mathf.Clamp(value, 16, 1024); }
        public int MaxRootPagesPerUpload { get => Mathf.Clamp(maxRootPagesPerUpload, 1, 64); set => maxRootPagesPerUpload = Mathf.Clamp(value, 1, 64); }
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
