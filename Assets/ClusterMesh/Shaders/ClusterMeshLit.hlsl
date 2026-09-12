#ifndef CLUSTERMESH_LIT_INCLUDED
#define CLUSTERMESH_LIT_INCLUDED

#pragma editor_sync_compilation

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(CLUSTERMESH_GBUFFER_PASS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#endif
#if defined(CLUSTERMESH_MOTION_VECTOR_PASS)
#include "ClusterMeshMotionVectors.hlsl"
#endif
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

// Populated by URP while rendering a shadow-caster pass. Directional lights
// use _LightDirection; punctual lights use _LightPosition per vertex.
float3 _LightDirection;
float3 _LightPosition;

StructuredBuffer<ClusterHeader> _Clusters;
StructuredBuffer<ClusterVertex> _Vertices;
StructuredBuffer<ClusterVertexTight> _VerticesTight;
StructuredBuffer<uint> _Indices;
StructuredBuffer<uint> _VisibleClusterIds;
StructuredBuffer<ClusterMeshObjectSH> _ObjectSH;
float _EnableClusterColor;
int _RestVertexTight;
StructuredBuffer<ClusterStreamAddress> _StreamAddresses;
StructuredBuffer<ClusterMeshStreamPageTableEntry> _StreamPageTable;
int _StreamPageVertexCapacity;
int _StreamPageIndexCapacity;
int _ClusterStreamingEnabled;

CBUFFER_START(ClusterMeshBatch)
    float4x4 _ObjectLocalToWorld[256];
    float4x4 _ObjectPreviousLocalToWorld[256];
    float4x4 _ObjectWorldToLocal[256];
    float _ObjectMotionVectorEnabled[256];
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
    nointerpolation uint objectIndex : TEXCOORD5;
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

half3 ClusterMeshSampleObjectSH(uint objectIndex, float3 normalWS)
{
    ClusterMeshObjectSH sh = _ObjectSH[objectIndex];
    float4 coeffs[7];
    coeffs[0] = sh.shAr;
    coeffs[1] = sh.shAg;
    coeffs[2] = sh.shAb;
    coeffs[3] = sh.shBr;
    coeffs[4] = sh.shBg;
    coeffs[5] = sh.shBb;
    coeffs[6] = float4(sh.shC.xyz, 0);
    return max(half3(0, 0, 0), SampleSH9(coeffs, normalWS));
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

    uint vertexBase = h.vertexOffset;
    uint indexOffset = h.indexOffset;
    if (_ClusterStreamingEnabled != 0)
    {
        ClusterStreamAddress address = _StreamAddresses[clusterId];
        ClusterMeshStreamPageTableEntry page = _StreamPageTable[address.pageId];
        if ((page.flags & 1u) == 0u)
        {
            positionOS = 0;
            normalOS = float3(0, 1, 0);
            tangentOS = float4(1, 0, 0, 1);
            uv = 0;
            return;
        }
        vertexBase = page.vertexBase + address.vertexOffset;
        indexOffset = address.indexOffset;
        uint streamIndex = indexOffset + vertexID;
        uint raw = _Indices[page.indexBase + (streamIndex >> 1)];
        uint localIndex = (streamIndex & 1u) == 0u ? (raw & 0xffffu) : (raw >> 16);
        uint vertexIndex = vertexBase + localIndex;
        if (_RestVertexTight != 0)
            ClusterMeshUnpackVertexTight(_VerticesTight[vertexIndex], positionOS, normalOS, tangentOS, uv);
        else
            ClusterMeshUnpackVertex(_Vertices[vertexIndex], positionOS, normalOS, tangentOS, uv);
        return;
    }

    uint raw = _Indices[(indexOffset + vertexID) >> 1];
    uint localIndex = ((indexOffset + vertexID) & 1u) == 0u
        ? (raw & 0xffffu)
        : (raw >> 16);
    uint vertexIndex = vertexBase + localIndex;
    if (_RestVertexTight != 0)
        ClusterMeshUnpackVertexTight(_VerticesTight[vertexIndex], positionOS, normalOS, tangentOS, uv);
    else
        ClusterMeshUnpackVertex(_Vertices[vertexIndex], positionOS, normalOS, tangentOS, uv);
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
    o.objectIndex = _VisibleClusterIds[input.instanceID] >> 16;
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
    inputData.fogCoord = _ObjectSH[input.objectIndex].shC.w > 0.5 ? ComputeFogFactor(input.positionCS.z) : 1;
    inputData.bakedGI = ClusterMeshSampleObjectSH(input.objectIndex, normalWS);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo.rgb;
    surface.metallic = _Metallic;
    surface.smoothness = _Smoothness;
    surface.normalTS = normalTS;
    surface.occlusion = 1;
    surface.alpha = albedo.a;
    return UniversalFragmentPBR(inputData, surface);
}

#if defined(CLUSTERMESH_GBUFFER_PASS)
FragmentOutput ClusterMeshGBufferFrag(Varyings input)
{
    half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
    clip(albedo.a - _Cutoff);
    if (_EnableClusterColor > 0.5f)
        albedo.rgb = ClusterMeshDebugRgb(input.clusterId);

    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    float3 nT = normalize(input.tangentWS.xyz);
    float3 nN = normalize(input.normalWS);
    float3 nB = cross(nN, nT) * input.tangentWS.w;

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalize(mul(normalTS, float3x3(nT, nB, nN)));
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    inputData.bakedGI = ClusterMeshSampleObjectSH(input.objectIndex, inputData.normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    BRDFData brdfData;
    InitializeBRDFData(albedo.rgb, _Metallic, half3(0, 0, 0), _Smoothness, albedo.a, brdfData);
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    half3 indirect = 0;
#if !defined(UNITY_TUANJIEGI)
    indirect = GlobalIllumination(brdfData, inputData.bakedGI, 1, inputData.positionWS,
        inputData.normalWS, inputData.viewDirectionWS);
#endif
    return BRDFDataToGbuffer(brdfData, inputData, _Smoothness, indirect, 1);
}
#endif

Varyings ClusterMeshShadowVert(Attributes input)
{
    float3 positionOS;
    float3 normalOS;
    float4 tangentOS;
    float2 uv;
    uint clusterId;
    ApplyClusterMeshInstance(input.instanceID);
    FetchClusterVertex(input.vertexID, input.instanceID, positionOS, normalOS, tangentOS, uv, clusterId);

    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    Varyings o;
    o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
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
    o.objectIndex = _VisibleClusterIds[input.instanceID] >> 16;
    return o;
}

half4 ClusterMeshShadowFrag(Varyings input) : SV_Target
{
    return 0;
}

#if defined(CLUSTERMESH_MOTION_VECTOR_PASS)
struct ClusterMeshMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
    nointerpolation float motionEnabled : TEXCOORD3;
};

ClusterMeshMotionVaryings ClusterMeshMotionVert(Attributes input)
{
    float3 positionOS;
    float3 normalOS;
    float4 tangentOS;
    float2 uv;
    uint clusterId;
    FetchClusterVertex(input.vertexID, input.instanceID, positionOS, normalOS, tangentOS, uv, clusterId);

    uint objectIndex = _VisibleClusterIds[input.instanceID] >> 16;
    float4 currentWS = mul(_ObjectLocalToWorld[objectIndex], float4(positionOS, 1.0));
    float4 previousWS = mul(_ObjectPreviousLocalToWorld[objectIndex], float4(positionOS, 1.0));

    ClusterMeshMotionVaryings output;
    output.positionCS = mul(UNITY_MATRIX_VP, currentWS);
#if defined(UNITY_REVERSED_Z)
    output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
#else
    output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
#endif
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, currentWS);
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, previousWS);
    output.uv = TRANSFORM_TEX(uv, _BaseMap);
    output.motionEnabled = _ObjectMotionVectorEnabled[objectIndex];
    return output;
}

half4 ClusterMeshMotionFrag(ClusterMeshMotionVaryings input) : SV_Target
{
    clip(input.motionEnabled - 0.5);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    clip(alpha - _Cutoff);
    return half4(CalcNdcMotionVectorFromCsPositions(
        input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
}
#endif
#endif
