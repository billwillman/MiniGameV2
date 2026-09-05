#ifndef CLUSTERMESH_LIT_INCLUDED
#define CLUSTERMESH_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "ClusterMeshBuffers.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _BumpScale;
    float _Metallic;
    float _Smoothness;
    float _Cutoff;
CBUFFER_END

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);

StructuredBuffer<ClusterHeader> _Clusters;
StructuredBuffer<ClusterVertex> _Vertices;
StructuredBuffer<uint> _Indices;
StructuredBuffer<uint> _VisibleClusterIds;
float _EnableClusterColor;

CBUFFER_START(ClusterMeshBatch)
    float4x4 _ObjectLocalToWorld[256];
    float4x4 _ObjectWorldToLocal[256];
CBUFFER_END

void ApplyClusterMeshInstance(uint instanceID)
{
    uint packed = _VisibleClusterIds[instanceID];
    uint objectIndex = packed >> 16;
    unity_ObjectToWorld = _ObjectLocalToWorld[objectIndex];
    unity_WorldToObject = _ObjectWorldToLocal[objectIndex];
}

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
void ClusterMeshSetup()
{
    ApplyClusterMeshInstance(unity_InstanceID);
}
#endif

struct Attributes
{
    uint vertexID : SV_VertexID;
    uint instanceID : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 uv : TEXCOORD3;
    nointerpolation uint clusterId : TEXCOORD4;
};

float3 ClusterMeshHsvToRgb(float h, float s, float v)
{
    if (s <= 0.0f)
        return v;
    float num = h * 6.0f;
    int sector = (int)floor(num);
    float f = num - sector;
    float p = v * (1.0f - s);
    float q = v * (1.0f - s * f);
    float t = v * (1.0f - s * (1.0f - f));
    if (sector == 0 || sector == 6)
        return float3(v, t, p);
    if (sector == 1)
        return float3(q, v, p);
    if (sector == 2)
        return float3(p, v, t);
    if (sector == 3)
        return float3(p, q, v);
    if (sector == 4)
        return float3(t, p, v);
    return float3(v, p, q);
}

float3 ClusterMeshDebugRgb(uint clusterId)
{
    float hue = frac((clusterId + 1.0f) * 0.6180339887f);
    return ClusterMeshHsvToRgb(hue, 0.72f, 0.95f);
}

void FetchClusterVertex(uint vertexID, uint instanceID, out float3 positionOS, out float3 normalOS, out float4 tangentOS, out float2 uv, out uint clusterId)
{
    uint packed = _VisibleClusterIds[instanceID];
    clusterId = packed & 0xFFFFu;
    ClusterHeader h = _Clusters[clusterId];
    if (vertexID >= h.triangleCount * 3)
    {
        positionOS = 0;
        normalOS = float3(0, 1, 0);
        tangentOS = float4(1, 0, 0, 1);
        uv = 0;
        return;
    }

    uint localIndex = _Indices[h.indexOffset + vertexID];
    ClusterVertex v = _Vertices[h.vertexOffset + localIndex];
    positionOS = v.position.xyz;
    normalOS = v.normal.xyz;
    tangentOS = v.tangent;
    uv = v.uv.xy;
}

Varyings ClusterMeshVert(Attributes input)
{
    float3 positionOS;
    float3 normalOS;
    float4 tangentOS;
    float2 uv;
    uint clusterId;
    ApplyClusterMeshInstance(input.instanceID);
    FetchClusterVertex(input.vertexID, input.instanceID, positionOS, normalOS, tangentOS, uv, clusterId);

    VertexPositionInputs pos = GetVertexPositionInputs(positionOS);
    VertexNormalInputs nrm = GetVertexNormalInputs(normalOS, tangentOS);

    Varyings o;
    o.positionCS = pos.positionCS;
    o.positionWS = pos.positionWS;
    o.normalWS = nrm.normalWS;
    o.tangentWS = float4(nrm.tangentWS, tangentOS.w);
    o.uv = TRANSFORM_TEX(uv, _BaseMap);
    o.clusterId = clusterId;
    return o;
}

half4 ClusterMeshFrag(Varyings input) : SV_Target
{
    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
    clip(albedo.a - _Cutoff);
    if (_EnableClusterColor > 0.5f)
        return half4(ClusterMeshDebugRgb(input.clusterId), albedo.a);

    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    float3 nT = normalize(input.tangentWS.xyz);
    float3 nN = normalize(input.normalWS);
    float3 nB = cross(nN, nT) * input.tangentWS.w;
    float3 normalWS = normalize(mul(normalTS, float3x3(nT, nB, nN)));

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    inputData.fogCoord = 0;
    inputData.bakedGI = SampleSH(normalWS);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo.rgb;
    surface.metallic = _Metallic;
    surface.smoothness = _Smoothness;
    surface.normalTS = normalTS;
    surface.occlusion = 1;
    surface.alpha = albedo.a;
    return UniversalFragmentPBR(inputData, surface);
}

Varyings ClusterMeshShadowVert(Attributes input)
{
    float3 positionOS;
    float3 normalOS;
    float4 tangentOS;
    float2 uv;
    uint clusterId;
    ApplyClusterMeshInstance(input.instanceID);
    FetchClusterVertex(input.vertexID, input.instanceID, positionOS, normalOS, tangentOS, uv, clusterId);
    Varyings o;
    o.positionCS = TransformWorldToHClip(TransformObjectToWorld(positionOS));
#if UNITY_REVERSED_Z
    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    o.positionWS = 0;
    o.normalWS = 0;
    o.tangentWS = 0;
    o.uv = uv;
    o.clusterId = clusterId;
    return o;
}

half4 ClusterMeshShadowFrag(Varyings input) : SV_Target
{
    return 0;
}
#endif
