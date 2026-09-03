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
        public uint pad0;
        public uint pad1;
        public uint pad2;
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
    public sealed class ClusterMeshBakeSettings
    {
        public int maxVerticesPerCluster = ClusterMeshLimits.MaxVerticesPerCluster;
        public int maxTrianglesPerCluster = ClusterMeshLimits.MaxTrianglesPerCluster;
    }

    public sealed class ClusterMeshBakeResult
    {
        public ClusterHeader[] clusters;
        public ClusterVertex[] vertices;
        public uint[] indices;
        public Material[] materials;
    }
}
