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
        public ClusterVertex[] vertices;
        public uint[] indices;
        public int hierarchyVersion;

        public void CopyFrom(ClusterMeshBakeResult result, Mesh source, ClusterMeshBakeSettings settings)
        {
            sourceMesh = source;
            materials = result.materials;
            maxVerticesPerCluster = settings.maxVerticesPerCluster;
            maxTrianglesPerCluster = settings.maxTrianglesPerCluster;
            clusters = result.clusters;
            vertices = result.vertices;
            indices = result.indices;
            hierarchyVersion = result.hierarchyVersion;
        }
    }
}
