#ifndef CLUSTERMESH_BUFFERS_INCLUDED
#define CLUSTERMESH_BUFFERS_INCLUDED

struct ClusterHeader
{
    uint vertexOffset;
    uint vertexCount;
    uint indexOffset;
    uint triangleCount;
    uint materialIndex;
    uint pad0;
    uint pad1;
    uint pad2;
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

#endif
