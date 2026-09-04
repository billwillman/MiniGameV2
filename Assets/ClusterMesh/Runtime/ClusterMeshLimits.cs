namespace ClusterMesh
{
    public static class ClusterMeshLimits
    {
        public const int MaxVerticesPerCluster = 64;
        public const int MaxTrianglesPerCluster = 124;
        public const int TemplateVertexCount = MaxTrianglesPerCluster * 3;
        public const int MaxBatchedObjects = 256;

        public static uint PackVisibleId(int objectIndex, int clusterIndex)
        {
            return ((uint)objectIndex << 16) | (uint)clusterIndex;
        }

        public static void UnpackVisibleId(uint packed, out int objectIndex, out int clusterIndex)
        {
            objectIndex = (int)(packed >> 16);
            clusterIndex = (int)(packed & 0xFFFFu);
        }
    }
}
