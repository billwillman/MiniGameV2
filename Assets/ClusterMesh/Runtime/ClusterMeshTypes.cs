using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ClusterMesh
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterHeader
    {
        public uint vertexOffset;
        public uint vertexCount;
        public uint indexOffset;
        public uint triangleCount;
        public uint materialIndex;
        public int parentIndex;
        public float lodError;
        public uint flags;
        public Vector4 aabbCenter;
        public Vector4 aabbExtents;
        public Vector4 coneAxisCutoff;
        public Vector4 coneApex;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterVertex
    {
        public Vector4 position;
        public Vector4 normal;
        public Vector4 tangent;
        public Vector4 uv;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterGroup
    {
        public int clusterStart;
        public int clusterCount;
        public int parentGroupIndex;
        public float lodError;
        public Vector4 aabbCenter;
        public Vector4 aabbExtents;
    }

    [Serializable]
    public sealed class ClusterMeshBakeSettings
    {
        public int maxVerticesPerCluster = ClusterMeshLimits.MaxVerticesPerCluster;
        public int maxTrianglesPerCluster = ClusterMeshLimits.MaxTrianglesPerCluster;
        public bool buildLodHierarchy = true;
    }

    public sealed class ClusterMeshBakeResult
    {
        public ClusterHeader[] clusters;
        public ClusterVertex[] vertices;
        public uint[] indices;
        public Material[] materials;
        public ClusterGroup[] groups;
        public int hierarchyVersion;
    }
}
