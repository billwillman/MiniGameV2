namespace ClusterMesh
{
    public static class ClusterMeshLimits
    {
        public const int MaxVerticesPerCluster = 64;
        public const int MaxTrianglesPerCluster = 124;
        public const int TemplateVertexCount = MaxTrianglesPerCluster * 3;
    }
}
