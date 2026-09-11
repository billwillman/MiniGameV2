using UnityEngine;

namespace ClusterMesh
{
    [CreateAssetMenu(menuName = "ClusterMesh/Cluster Mesh Asset", fileName = "ClusterMeshAsset")]
    [PreferBinarySerialization]
    public sealed class ClusterMeshAsset : ScriptableObject
    {
        public Mesh sourceMesh;
        public Material[] materials;
        public int maxVerticesPerCluster = ClusterMeshLimits.MaxVerticesPerCluster;
        public int maxTrianglesPerCluster = ClusterMeshLimits.MaxTrianglesPerCluster;
        public ClusterHeader[] clusters;
        public ClusterGroup[] groups;
        public int hierarchyVersion;
        public int geometryVersion;
        public int vertexStride;
        public int vertexCount;
        public int indexCount;
        public byte[] packedVertices;
        public byte[] packedIndices;

        public int ResolvedVertexStride =>
            vertexStride > 0 ? vertexStride : ClusterMeshLimits.ClusterVertexStride;

        public void CopyFrom(ClusterMeshBakeResult result, Mesh source, ClusterMeshBakeSettings settings)
        {
            CopyFrom(result, source, settings, settings != null && settings.packTightRestVertices);
        }

        public void CopyFrom(
            ClusterMeshBakeResult result, Mesh source, ClusterMeshBakeSettings settings, bool tightRestVertices)
        {
            sourceMesh = source;
            materials = result.materials;
            maxVerticesPerCluster = settings.maxVerticesPerCluster;
            maxTrianglesPerCluster = settings.maxTrianglesPerCluster;
            clusters = result.clusters;
            groups = result.groups;
            hierarchyVersion = result.hierarchyVersion;
            ClusterMeshGeometry.WritePacked(this, result, tightRestVertices);
        }

        public void CopyPackedFrom(ClusterMeshAsset other)
        {
            sourceMesh = other.sourceMesh;
            materials = other.materials;
            maxVerticesPerCluster = other.maxVerticesPerCluster;
            maxTrianglesPerCluster = other.maxTrianglesPerCluster;
            clusters = other.clusters;
            groups = other.groups;
            hierarchyVersion = other.hierarchyVersion;
            geometryVersion = other.geometryVersion;
            vertexStride = other.vertexStride;
            vertexCount = other.vertexCount;
            indexCount = other.indexCount;
            packedVertices = other.packedVertices;
            packedIndices = other.packedIndices;
        }
    }
}
