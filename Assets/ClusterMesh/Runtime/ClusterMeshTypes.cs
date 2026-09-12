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
    public struct ClusterPackedVertex
    {
        public Vector4 position;
        public uint nrmXY;
        public uint nrmZ_tanW;
        public uint tanXY;
        public uint uv;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterPackedVertexTight
    {
        public float px;
        public float py;
        public float pz;
        public uint nrmOct;
        public uint tanOctTanW;
        public uint uv;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct ClusterMeshObjectSH
    {
        public Vector4 shAr;
        public Vector4 shAg;
        public Vector4 shAb;
        public Vector4 shBr;
        public Vector4 shBg;
        public Vector4 shBb;
        public Vector4 shC;
    }

    [Serializable]
    public sealed class ClusterMeshBakeSettings
    {
        public int maxVerticesPerCluster = ClusterMeshLimits.MaxVerticesPerCluster;
        public int maxTrianglesPerCluster = ClusterMeshLimits.MaxTrianglesPerCluster;
        public bool buildLodHierarchy = true;
        public bool useQemSimplify = true;
        public bool packTightRestVertices;
        [Tooltip("Write geometry into a page-streamed sidecar file. Disabled keeps the legacy embedded asset format unchanged.")]
        public bool enableStreaming;
        [Range(8, 512)]
        [Tooltip("Maximum physical GPU page slots per streamed asset. Root LOD pages are always resident and may raise this minimum.")]
        public int streamingPagePoolCapacity = 64;
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
