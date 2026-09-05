#ifndef CLUSTERMESH_BUFFERS_INCLUDED
#define CLUSTERMESH_BUFFERS_INCLUDED

struct ClusterHeader
{
    uint vertexOffset;
    uint vertexCount;
    uint indexOffset;
    uint triangleCount;
    uint materialIndex;
    int parentIndex;
    float lodError;
    uint flags;
    float4 aabbCenter;
    float4 aabbExtents;
    float4 coneAxisCutoff;
    float4 coneApex;
};

struct ClusterVertex
{
    float4 position;
    float4 normal;
    float4 tangent;
    float4 uv;
};

struct ClusterGroup
{
    int clusterStart;
    int clusterCount;
    int parentGroupIndex;
    float lodError;
    float4 aabbCenter;
    float4 aabbExtents;
};

#endif
