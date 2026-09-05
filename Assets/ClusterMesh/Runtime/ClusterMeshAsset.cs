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
        public int vertexCount;
        public int indexCount;
        public byte[] packedVertices;
        public byte[] packedIndices;

        public void CopyFrom(ClusterMeshBakeResult result, Mesh source, ClusterMeshBakeSettings settings)
        {
            sourceMesh = source;
            materials = result.materials;
            maxVerticesPerCluster = settings.maxVerticesPerCluster;
            maxTrianglesPerCluster = settings.maxTrianglesPerCluster;
            clusters = result.clusters;
            groups = result.groups;
            hierarchyVersion = result.hierarchyVersion;
            ClusterMeshGeometry.WritePacked(this, result);
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
            vertexCount = other.vertexCount;
            indexCount = other.indexCount;
            packedVertices = other.packedVertices;
            packedIndices = other.packedIndices;
        }
    }
}
