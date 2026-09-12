#ifndef CLUSTER_SKINNED_MESH_LIT_INCLUDED
#define CLUSTER_SKINNED_MESH_LIT_INCLUDED

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
float4 _BaseMap_ST, _BaseColor;
float _BumpScale, _Metallic, _Smoothness, _Cutoff;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
Texture2D<float4> _SkinPaletteTex;
Texture2D<float4> _SkinPreviousPaletteTex;
Texture2D<float4> _SkinAnimationTex;
int _SkinPaletteWidth;
int _UseGpuAnimationTexture;
int _SkinAnimationFrameCount;
int _SkinWeightPacked8;
int _RestVertexTight;
int _GpuPalettePixelsPerBone;
float3 _LightDirection, _LightPosition;

struct ClusterPackedSkinWeight { uint boneIndices01, boneIndices23, boneWeights01, boneWeights23; };
StructuredBuffer<ClusterHeader> _Clusters;
StructuredBuffer<ClusterVertex> _Vertices;
StructuredBuffer<ClusterVertexTight> _VerticesTight;
StructuredBuffer<uint> _Indices;
StructuredBuffer<ClusterPackedSkinWeight> _SkinWeights;
StructuredBuffer<uint> _SkinWeights8;
StructuredBuffer<uint> _VisibleClusterIds;
StructuredBuffer<ClusterMeshObjectSH> _ObjectSH;
StructuredBuffer<float> _ObjectAnimationTimes;
StructuredBuffer<float> _PreviousObjectAnimationTimes;
StructuredBuffer<ClusterStreamAddress> _StreamAddresses;
StructuredBuffer<ClusterMeshStreamPageTableEntry> _StreamPageTable;
int _StreamPageVertexCapacity;
int _StreamPageIndexCapacity;
int _ClusterStreamingEnabled;
CBUFFER_START(ClusterSkinnedBatch)
float4x4 _ObjectLocalToWorld[256];
float4x4 _ObjectPreviousLocalToWorld[256];
float4x4 _ObjectWorldToLocal[256];
float _ObjectMotionVectorEnabled[256];
CBUFFER_END
float _EnableClusterColor;

struct Attributes { uint vertexID : SV_VertexID; uint instanceID : SV_InstanceID; };
struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; nointerpolation uint clusterId : TEXCOORD4; nointerpolation uint objectIndex : TEXCOORD5; };

half3 ClusterSkinnedSampleObjectSH(uint objectIndex, float3 normalWS)
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

float3 ClusterSkinnedHsvToRgb(float h, float s, float v)
{
    if (s <= 0.0f) return v;
    float num = h * 6.0f;
    int sector = (int)floor(num);
    float f = num - sector;
    float p = v * (1.0f - s);
    float q = v * (1.0f - s * f);
    float t = v * (1.0f - s * (1.0f - f));
    if (sector == 0 || sector == 6) return float3(v, t, p);
    if (sector == 1) return float3(q, v, p);
    if (sector == 2) return float3(p, v, t);
    if (sector == 3) return float3(p, q, v);
    if (sector == 4) return float3(t, p, v);
    return float3(v, p, q);
}

float3 ClusterSkinnedDebugRgb(uint clusterId)
{
    float hue = frac((clusterId + 1.0f) * 0.6180339887f);
    return ClusterSkinnedHsvToRgb(hue, 0.72f, 0.95f);
}

void ApplyClusterSkinnedInstance(uint instanceID)
{
    uint objectIndex = _VisibleClusterIds[instanceID] >> 16;
    unity_ObjectToWorld = _ObjectLocalToWorld[objectIndex];
    unity_WorldToObject = _ObjectWorldToLocal[objectIndex];
}

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
void ClusterSkinnedSetup()
{
    ApplyClusterSkinnedInstance(unity_InstanceID);
}
#endif
float2 UnpackHalf2(uint v) { return ClusterMeshUnpackHalf2(v); }
float3x3 QuatToMat(float4 q)
{
    q = normalize(q);
    float x = q.x, y = q.y, z = q.z, w = q.w;
    float x2 = x + x, y2 = y + y, z2 = z + z;
    float xx = x * x2, xy = x * y2, xz = x * z2;
    float yy = y * y2, yz = y * z2, zz = z * z2;
    float wx = w * x2, wy = w * y2, wz = w * z2;
    return float3x3(
        1.0 - (yy + zz), xy - wz, xz + wy,
        xy + wz, 1.0 - (xx + zz), yz - wx,
        xz - wy, yz + wx, 1.0 - (xx + yy));
}
void FetchBaseVertex(uint vertexID, uint instanceID, out float3 p, out float3 n, out float4 t, out float2 uv, out uint vertexIndex)
{
    // Tuanjie/DXC does not always propagate definite assignment through an
    // out-parameter helper. Seed every output before the tight-vertex branch.
    p = float3(0.0, 0.0, 0.0);
    n = float3(0.0, 1.0, 0.0);
    t = float4(1.0, 0.0, 0.0, 1.0);
    uv = float2(0.0, 0.0);
    vertexIndex = 0u;
    uint cluster = _VisibleClusterIds[instanceID] & 0xffffu;
    ClusterHeader h = _Clusters[cluster];
    if (vertexID >= h.triangleCount * 3u) return;
    uint vertexBufferIndex;
    if (_ClusterStreamingEnabled != 0)
    {
        ClusterStreamAddress address = _StreamAddresses[cluster];
        ClusterMeshStreamPageTableEntry page = _StreamPageTable[address.pageId];
        if ((page.flags & 1u) == 0u) return;
        uint streamIndex = address.indexOffset + vertexID;
        uint raw = _Indices[page.indexBase + (streamIndex >> 1)];
        uint local = (streamIndex & 1u) == 0 ? raw & 0xffffu : raw >> 16;
        vertexBufferIndex = page.vertexBase + address.vertexOffset + local;
        // _SkinWeights lives in the physical streamed weight range. This is
        // deliberately separate from vertexBase when the page is compacted.
        vertexIndex = page.weightBase + address.vertexOffset + local;
    }
    else
    {
        uint raw = _Indices[(h.indexOffset + vertexID) >> 1];
        uint local = ((h.indexOffset + vertexID) & 1u) == 0 ? raw & 0xffffu : raw >> 16;
        vertexBufferIndex = h.vertexOffset + local;
        vertexIndex = vertexBufferIndex;
    }
    if (_RestVertexTight != 0)
    {
        ClusterMeshUnpackVertexTight(_VerticesTight[vertexBufferIndex], p, n, t, uv);
        return;
    }
    ClusterVertex v = _Vertices[vertexBufferIndex];
    p = v.position.xyz;
    float2 nxy = UnpackHalf2(v.nrmXY), nztw = UnpackHalf2(v.nrmZ_tanW), txy = UnpackHalf2(v.tanXY);
    n = normalize(float3(nxy.x,nxy.y,nztw.x));
    float tz = abs(n.z) > 1e-4 ? -(n.x*txy.x+n.y*txy.y)/n.z : v.position.w*sqrt(max(0,1-dot(txy,txy)));
    t = float4(normalize(float3(txy.x,txy.y,tz)-n*dot(n,float3(txy.x,txy.y,tz))), nztw.y);
    uv = UnpackHalf2(v.uv);
}
float4 CompactPaletteRowAtTime(uint bone, uint row, float normalizedTime)
{
    uint lastFrame = (uint)max(_SkinAnimationFrameCount - 1, 0);
    float frame = saturate(normalizedTime) * lastFrame;
    uint frame0 = min((uint)floor(frame), lastFrame);
    uint frame1 = min(frame0 + 1u, lastFrame);
    float blend = frac(frame);
    uint xq = bone * 2u;
    float4 q0 = _SkinAnimationTex.Load(int3(xq, frame0, 0));
    float4 q1 = _SkinAnimationTex.Load(int3(xq, frame1, 0));
    if (dot(q0, q1) < 0.0) q1 = -q1;
    float4 q = normalize(lerp(q0, q1, blend));
    float4 ts = lerp(
        _SkinAnimationTex.Load(int3(xq + 1u, frame0, 0)),
        _SkinAnimationTex.Load(int3(xq + 1u, frame1, 0)),
        blend);
    float3x3 r = QuatToMat(q) * ts.w;
    float4 result = float4(r[2][0], r[2][1], r[2][2], ts.z);
    if (row == 0u)
        result = float4(r[0][0], r[0][1], r[0][2], ts.x);
    else if (row == 1u)
        result = float4(r[1][0], r[1][1], r[1][2], ts.y);
    return result;
}

float4 GpuPaletteRowAtTime(uint bone, uint row, float normalizedTime)
{
    if (_GpuPalettePixelsPerBone == 2)
        return CompactPaletteRowAtTime(bone, row, normalizedTime);

    uint x = bone * 3u + row;
    uint lastFrame = (uint)max(_SkinAnimationFrameCount - 1, 0);
    float frame = saturate(normalizedTime) * lastFrame;
    uint frame0 = min((uint)floor(frame), lastFrame);
    uint frame1 = min(frame0 + 1u, lastFrame);
    return lerp(
        _SkinAnimationTex.Load(int3(x, frame0, 0)),
        _SkinAnimationTex.Load(int3(x, frame1, 0)),
        frac(frame));
}

float4 PaletteRow(uint objectIndex, uint bone, uint row)
{
    if (_UseGpuAnimationTexture != 0)
        return GpuPaletteRowAtTime(bone, row, _ObjectAnimationTimes[objectIndex]);
    return _SkinPaletteTex.Load(int3(bone * 3u + row, objectIndex, 0));
}

float4 PreviousPaletteRow(uint objectIndex, uint bone, uint row)
{
    if (_UseGpuAnimationTexture != 0)
        return GpuPaletteRowAtTime(bone, row, _PreviousObjectAnimationTimes[objectIndex]);
    return _SkinPreviousPaletteTex.Load(int3(bone * 3u + row, objectIndex, 0));
}

void FetchSkinInfluences(uint index, out uint4 bones, out float4 weights)
{
    if (_SkinWeightPacked8 != 0)
    {
        uint baseIndex = index * 2u;
        uint bi = _SkinWeights8[baseIndex];
        uint bw = _SkinWeights8[baseIndex + 1u];
        bones = uint4(bi & 255u, (bi >> 8) & 255u, (bi >> 16) & 255u, bi >> 24);
        weights = float4(bw & 255u, (bw >> 8) & 255u, (bw >> 16) & 255u, bw >> 24) / 255.0;
    }
    else
    {
        ClusterPackedSkinWeight skinWeight = _SkinWeights[index];
        bones = uint4(
            skinWeight.boneIndices01 & 0xffffu, skinWeight.boneIndices01 >> 16,
            skinWeight.boneIndices23 & 0xffffu, skinWeight.boneIndices23 >> 16);
        weights = float4(
            skinWeight.boneWeights01 & 0xffffu, skinWeight.boneWeights01 >> 16,
            skinWeight.boneWeights23 & 0xffffu, skinWeight.boneWeights23 >> 16) / 65535.0;
    }
    weights /= max(dot(weights, 1), 1e-6);
}
float3 TransformPalettePoint(uint objectIndex, uint bone, float3 p)
{
    float4 v=float4(p,1); return float3(dot(PaletteRow(objectIndex,bone,0),v),dot(PaletteRow(objectIndex,bone,1),v),dot(PaletteRow(objectIndex,bone,2),v));
}
float3 TransformPaletteVector(uint objectIndex, uint bone, float3 p)
{
    float4 v=float4(p,0); return float3(dot(PaletteRow(objectIndex,bone,0),v),dot(PaletteRow(objectIndex,bone,1),v),dot(PaletteRow(objectIndex,bone,2),v));
}
float3 TransformPreviousPalettePoint(uint objectIndex, uint bone, float3 p)
{
    float4 v=float4(p,1); return float3(dot(PreviousPaletteRow(objectIndex,bone,0),v),dot(PreviousPaletteRow(objectIndex,bone,1),v),dot(PreviousPaletteRow(objectIndex,bone,2),v));
}
void SkinVertex(uint objectIndex, uint index, inout float3 p, inout float3 n, inout float4 t)
{
    uint4 bones;
    float4 weights;
    FetchSkinInfluences(index, bones, weights);
    float3 sp=0,sn=0,st=0;
    [unroll] for(uint i=0;i<4;i++) { sp+=TransformPalettePoint(objectIndex,bones[i],p)*weights[i]; sn+=TransformPaletteVector(objectIndex,bones[i],n)*weights[i]; st+=TransformPaletteVector(objectIndex,bones[i],t.xyz)*weights[i]; }
    p=sp; n=normalize(sn); t.xyz=normalize(st-n*dot(n,st));
}

float3 SkinPreviousPosition(uint objectIndex, uint index, float3 positionOS)
{
    uint4 bones;
    float4 weights;
    FetchSkinInfluences(index, bones, weights);
    float3 result = 0;
    [unroll] for (uint i = 0; i < 4; i++)
        result += TransformPreviousPalettePoint(objectIndex, bones[i], positionOS) * weights[i];
    return result;
}
Varyings ClusterSkinnedVert(Attributes input)
{
    uint objectIndex=_VisibleClusterIds[input.instanceID]>>16, vertexIndex; float3 p,n; float4 t; float2 uv;
    ApplyClusterSkinnedInstance(input.instanceID); FetchBaseVertex(input.vertexID,input.instanceID,p,n,t,uv,vertexIndex); SkinVertex(objectIndex,vertexIndex,p,n,t);
    VertexPositionInputs pos=GetVertexPositionInputs(p); VertexNormalInputs normal=GetVertexNormalInputs(n,t);
    Varyings o; o.positionCS=pos.positionCS; o.positionWS=pos.positionWS; o.normalWS=normal.normalWS; o.tangentWS=float4(normal.tangentWS,t.w); o.uv=TRANSFORM_TEX(uv,_BaseMap); o.clusterId=_VisibleClusterIds[input.instanceID]&0xffffu; o.objectIndex=objectIndex; return o;
}
half4 ClusterSkinnedFrag(Varyings i):SV_Target
{
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor; clip(albedo.a-_Cutoff);
    if (_EnableClusterColor > 0.5f) return half4(ClusterSkinnedDebugRgb(i.clusterId), albedo.a);
    float3 nts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale); float3 t=normalize(i.tangentWS.xyz),n=normalize(i.normalWS),b=cross(n,t)*i.tangentWS.w;
    InputData d=(InputData)0; d.positionWS=i.positionWS; d.normalWS=normalize(mul(nts,float3x3(t,b,n))); d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); d.shadowCoord=TransformWorldToShadowCoord(i.positionWS); d.fogCoord=_ObjectSH[i.objectIndex].shC.w>0.5?ComputeFogFactor(i.positionCS.z):1; d.bakedGI=ClusterSkinnedSampleObjectSH(i.objectIndex,d.normalWS);
    SurfaceData s=(SurfaceData)0; s.albedo=albedo.rgb;s.metallic=_Metallic;s.smoothness=_Smoothness;s.normalTS=nts;s.occlusion=1;s.alpha=albedo.a; return UniversalFragmentPBR(d,s);
}
#if defined(CLUSTERMESH_GBUFFER_PASS)
FragmentOutput ClusterSkinnedGBufferFrag(Varyings i)
{
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor; clip(albedo.a-_Cutoff);
    if (_EnableClusterColor > 0.5f) albedo.rgb=ClusterSkinnedDebugRgb(i.clusterId);
    float3 nts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
    float3 t=normalize(i.tangentWS.xyz),n=normalize(i.normalWS),b=cross(n,t)*i.tangentWS.w;
    InputData d=(InputData)0; d.positionWS=i.positionWS; d.positionCS=i.positionCS;
    d.normalWS=normalize(mul(nts,float3x3(t,b,n))); d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
    d.shadowCoord=TransformWorldToShadowCoord(i.positionWS); d.bakedGI=ClusterSkinnedSampleObjectSH(i.objectIndex,d.normalWS);
    d.shadowMask=half4(1,1,1,1); d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
    BRDFData brdf; InitializeBRDFData(albedo.rgb,_Metallic,half3(0,0,0),_Smoothness,albedo.a,brdf);
    Light mainLight=GetMainLight(d.shadowCoord,d.positionWS,d.shadowMask);
    MixRealtimeAndBakedGI(mainLight,d.normalWS,d.bakedGI,d.shadowMask);
    half3 indirect=0;
    #if !defined(UNITY_TUANJIEGI)
    indirect=GlobalIllumination(brdf,d.bakedGI,1,d.positionWS,d.normalWS,d.viewDirectionWS);
    #endif
    return BRDFDataToGbuffer(brdf,d,_Smoothness,indirect,1);
}
#endif
Varyings ClusterSkinnedShadowVert(Attributes input)
{
    uint objectIndex=_VisibleClusterIds[input.instanceID]>>16, vertexIndex; float3 p,n; float4 t; float2 uv;
    ApplyClusterSkinnedInstance(input.instanceID); FetchBaseVertex(input.vertexID,input.instanceID,p,n,t,uv,vertexIndex); SkinVertex(objectIndex,vertexIndex,p,n,t);
    float3 ws=TransformObjectToWorld(p), nw=TransformObjectToWorldNormal(n);
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 ld=normalize(_LightPosition-ws);
    #else
    float3 ld=_LightDirection;
    #endif
    Varyings o=(Varyings)0; o.positionCS=TransformWorldToHClip(ApplyShadowBias(ws,nw,ld));
    #if UNITY_REVERSED_Z
    o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #else
    o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
    #endif
    o.uv=TRANSFORM_TEX(uv,_BaseMap); return o;
}
Varyings ClusterSkinnedDepthVert(Attributes input)
{
    uint objectIndex=_VisibleClusterIds[input.instanceID]>>16, vertexIndex; float3 p,n; float4 t; float2 uv;
    ApplyClusterSkinnedInstance(input.instanceID); FetchBaseVertex(input.vertexID,input.instanceID,p,n,t,uv,vertexIndex); SkinVertex(objectIndex,vertexIndex,p,n,t);
    Varyings o=(Varyings)0; o.positionCS=TransformObjectToHClip(p); o.uv=TRANSFORM_TEX(uv,_BaseMap); return o;
}
half4 ClusterSkinnedShadowFrag(Varyings i):SV_Target { half a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a*_BaseColor.a; clip(a-_Cutoff); return 0; }

#if defined(CLUSTERMESH_MOTION_VECTOR_PASS)
struct ClusterSkinnedMotionVaryings
{
    float4 positionCS : SV_POSITION;
    float4 positionCSNoJitter : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv : TEXCOORD2;
    nointerpolation float motionEnabled : TEXCOORD3;
};

ClusterSkinnedMotionVaryings ClusterSkinnedMotionVert(Attributes input)
{
    uint objectIndex = _VisibleClusterIds[input.instanceID] >> 16;
    uint vertexIndex;
    float3 restPosition, normalOS;
    float4 tangentOS;
    float2 uv;
    FetchBaseVertex(input.vertexID, input.instanceID, restPosition, normalOS, tangentOS, uv, vertexIndex);

    float3 currentPosition = restPosition;
    SkinVertex(objectIndex, vertexIndex, currentPosition, normalOS, tangentOS);
    float3 previousPosition = SkinPreviousPosition(objectIndex, vertexIndex, restPosition);
    float4 currentWS = mul(_ObjectLocalToWorld[objectIndex], float4(currentPosition, 1.0));
    float4 previousWS = mul(_ObjectPreviousLocalToWorld[objectIndex], float4(previousPosition, 1.0));

    ClusterSkinnedMotionVaryings output;
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

half4 ClusterSkinnedMotionFrag(ClusterSkinnedMotionVaryings input) : SV_Target
{
    clip(input.motionEnabled - 0.5);
    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
    clip(alpha - _Cutoff);
    return half4(CalcNdcMotionVectorFromCsPositions(
        input.positionCSNoJitter, input.previousPositionCSNoJitter), 0, 0);
}
#endif
#endif
