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
    uint nrmXY;
    uint nrmZ_tanW;
    uint tanXY;
    uint uv;
};

float2 ClusterMeshUnpackHalf2(uint packed)
{
    return float2(f16tof32(packed & 0xffffu), f16tof32(packed >> 16));
}

void ClusterMeshUnpackVertex(ClusterVertex v, out float3 positionOS, out float3 normalOS, out float4 tangentOS, out float2 uv)
{
    positionOS = v.position.xyz;
    float2 nxy = ClusterMeshUnpackHalf2(v.nrmXY);
    float2 nztw = ClusterMeshUnpackHalf2(v.nrmZ_tanW);
    float2 txy = ClusterMeshUnpackHalf2(v.tanXY);
    uv = ClusterMeshUnpackHalf2(v.uv);
    float3 n = normalize(float3(nxy.x, nxy.y, nztw.x));
    float tz;
    if (abs(n.z) >= 1e-4)
        tz = -(n.x * txy.x + n.y * txy.y) / n.z;
    else
        tz = v.position.w * sqrt(max(0.0, 1.0 - txy.x * txy.x - txy.y * txy.y));
    float3 t = float3(txy.x, txy.y, tz);
    t = normalize(t - n * dot(n, t));
    normalOS = n;
    tangentOS = float4(t, nztw.y);
}

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
