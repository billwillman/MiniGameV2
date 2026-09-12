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

// 6 uints = 24 bytes. Do not use float3 — HLSL pads it to 16.
struct ClusterVertexTight
{
    uint px;
    uint py;
    uint pz;
    uint nrmOct;
    uint tanOctTanW;
    uint uv;
};

float3 ClusterMeshOctDecode16(uint packed)
{
    float ox = ((packed & 0xffffu) / 65535.0) * 2.0 - 1.0;
    float oy = ((packed >> 16) / 65535.0) * 2.0 - 1.0;
    float3 n = float3(ox, oy, 1.0 - abs(ox) - abs(oy));
    float t = max(-n.z, 0.0);
    n.x += n.x >= 0.0 ? -t : t;
    n.y += n.y >= 0.0 ? -t : t;
    return normalize(n);
}

void ClusterMeshOctDecodeTan(uint packed, out float3 t, out float tanW)
{
    uint x = packed & 0xffffu;
    uint y = (packed >> 16) & 0x7fffu;
    tanW = (packed & 0x80000000u) != 0u ? 1.0 : -1.0;
    uint oct = x | ((uint)round(y * (65535.0 / 32767.0)) << 16);
    t = ClusterMeshOctDecode16(oct);
}

void ClusterMeshUnpackVertexTight(ClusterVertexTight v, out float3 positionOS, out float3 normalOS, out float4 tangentOS, out float2 uv)
{
    positionOS = float3(asfloat(v.px), asfloat(v.py), asfloat(v.pz));
    float3 n = ClusterMeshOctDecode16(v.nrmOct);
    float3 tt;
    float tanW;
    ClusterMeshOctDecodeTan(v.tanOctTanW, tt, tanW);
    tt = normalize(tt - n * dot(n, tt));
    normalOS = n;
    tangentOS = float4(tt, tanW);
    uv = ClusterMeshUnpackHalf2(v.uv);
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

// 64 bytes. Keep this layout in sync with the streaming node GraphicsBuffer.
// The explicit scalar fields before the two float4s avoid platform-dependent
// float3 alignment in StructuredBuffers.
struct ClusterStreamNode
{
    int clusterStart;
    int clusterCount;
    int parentNodeIndex;
    int materialIndex;
    float lodError;
    int lodLevel;
    uint flags;
    uint padding;
    float4 aabbCenter;
    float4 aabbExtents;
};

struct ClusterStreamAddress
{
    uint pageId;
    uint vertexOffset;
    uint indexOffset;
    uint reserved;
};

// 16 bytes. A logical stream page maps to independently packed physical
// vertex, packed-index and skin-weight ranges.
struct ClusterMeshStreamPageTableEntry
{
    uint vertexBase;
    uint indexBase;
    uint weightBase;
    uint flags;
};

struct ClusterMeshObjectSH
{
    float4 shAr;
    float4 shAg;
    float4 shAb;
    float4 shBr;
    float4 shBg;
    float4 shBb;
    float4 shC;
};

#endif
