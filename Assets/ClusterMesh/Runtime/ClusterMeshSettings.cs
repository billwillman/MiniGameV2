using UnityEngine;

namespace ClusterMesh
{
    public sealed class ClusterMeshSettings : ScriptableObject
    {
        public const string ResourceName = "ClusterMeshSettings";
        public const string AssetPath = "Assets/ClusterMesh/Resources/ClusterMeshSettings.asset";

        [SerializeField]
        ClusterMeshMotionVectorSlot motionVectorSlot = ClusterMeshMotionVectorSlot.AfterSkyboxPlus1;

        public ClusterMeshMotionVectorSlot MotionVectorSlot
        {
            get => motionVectorSlot;
            set => motionVectorSlot = value;
        }

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
