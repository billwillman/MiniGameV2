#ifndef CLUSTER_SKINNED_MESH_LIT_INCLUDED
#define CLUSTER_SKINNED_MESH_LIT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "ClusterMeshBuffers.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST, _BaseColor;
float _BumpScale, _Metallic, _Smoothness, _Cutoff;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
Texture2D<float4> _SkinPaletteTex;
Texture2D<float4> _SkinAnimationTex;
int _SkinPaletteWidth;
int _UseGpuAnimationTexture;
int _SkinAnimationFrameCount;
float3 _LightDirection, _LightPosition;

struct ClusterPackedSkinWeight { uint boneIndices01, boneIndices23, boneWeights01, boneWeights23; };
StructuredBuffer<ClusterHeader> _Clusters;
StructuredBuffer<ClusterVertex> _Vertices;
StructuredBuffer<uint> _Indices;
StructuredBuffer<ClusterPackedSkinWeight> _SkinWeights;
StructuredBuffer<uint> _VisibleClusterIds;
StructuredBuffer<float> _ObjectAnimationTimes;
CBUFFER_START(ClusterSkinnedBatch)
float4x4 _ObjectLocalToWorld[256];
float4x4 _ObjectWorldToLocal[256];
CBUFFER_END
float _EnableClusterColor;

struct Attributes { uint vertexID : SV_VertexID; uint instanceID : SV_InstanceID; };
struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float4 tangentWS : TEXCOORD2; float2 uv : TEXCOORD3; nointerpolation uint clusterId : TEXCOORD4; };

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
float2 UnpackHalf2(uint v) { return float2(f16tof32(v & 0xffffu), f16tof32(v >> 16)); }
void FetchBaseVertex(uint vertexID, uint instanceID, out float3 p, out float3 n, out float4 t, out float2 uv, out uint vertexIndex)
{
    uint cluster = _VisibleClusterIds[instanceID] & 0xffffu;
    ClusterHeader h = _Clusters[cluster];
    if (vertexID >= h.triangleCount * 3) { p=0; n=float3(0,1,0); t=float4(1,0,0,1); uv=0; vertexIndex=0; return; }
    uint raw = _Indices[(h.indexOffset + vertexID) >> 1];
    uint local = ((h.indexOffset + vertexID) & 1u) == 0 ? raw & 0xffffu : raw >> 16;
    vertexIndex = h.vertexOffset + local;
    ClusterVertex v = _Vertices[vertexIndex];
    p = v.position.xyz;
    float2 nxy = UnpackHalf2(v.nrmXY), nztw = UnpackHalf2(v.nrmZ_tanW), txy = UnpackHalf2(v.tanXY);
    n = normalize(float3(nxy.x,nxy.y,nztw.x));
    float tz = abs(n.z) > 1e-4 ? -(n.x*txy.x+n.y*txy.y)/n.z : v.position.w*sqrt(max(0,1-dot(txy,txy)));
    t = float4(normalize(float3(txy.x,txy.y,tz)-n*dot(n,float3(txy.x,txy.y,tz))), nztw.y);
    uv = UnpackHalf2(v.uv);
}
float4 PaletteRow(uint objectIndex, uint bone, uint row)
{
    uint x = bone * 3u + row;
    if (_UseGpuAnimationTexture != 0)
    {
        uint lastFrame = (uint)max(_SkinAnimationFrameCount - 1, 0);
        float frame = saturate(_ObjectAnimationTimes[objectIndex]) * lastFrame;
        uint frame0 = min((uint)floor(frame), lastFrame);
        uint frame1 = min(frame0 + 1u, lastFrame);
        return lerp(
            _SkinAnimationTex.Load(int3(x, frame0, 0)),
            _SkinAnimationTex.Load(int3(x, frame1, 0)),
            frac(frame));
    }
    return _SkinPaletteTex.Load(int3(x, objectIndex, 0));
}
float3 TransformPalettePoint(uint objectIndex, uint bone, float3 p)
{
    float4 v=float4(p,1); return float3(dot(PaletteRow(objectIndex,bone,0),v),dot(PaletteRow(objectIndex,bone,1),v),dot(PaletteRow(objectIndex,bone,2),v));
}
float3 TransformPaletteVector(uint objectIndex, uint bone, float3 p)
{
    float4 v=float4(p,0); return float3(dot(PaletteRow(objectIndex,bone,0),v),dot(PaletteRow(objectIndex,bone,1),v),dot(PaletteRow(objectIndex,bone,2),v));
}
void SkinVertex(uint objectIndex, uint index, inout float3 p, inout float3 n, inout float4 t)
{
    ClusterPackedSkinWeight w = _SkinWeights[index];
    uint4 bones=uint4(w.boneIndices01 & 0xffffu,w.boneIndices01>>16,w.boneIndices23 & 0xffffu,w.boneIndices23>>16);
    float4 weights=float4(w.boneWeights01 & 0xffffu,w.boneWeights01>>16,w.boneWeights23 & 0xffffu,w.boneWeights23>>16)/65535.0;
    float sum=max(dot(weights,1),1e-6); weights/=sum;
    float3 sp=0,sn=0,st=0;
    [unroll] for(uint i=0;i<4;i++) { sp+=TransformPalettePoint(objectIndex,bones[i],p)*weights[i]; sn+=TransformPaletteVector(objectIndex,bones[i],n)*weights[i]; st+=TransformPaletteVector(objectIndex,bones[i],t.xyz)*weights[i]; }
    p=sp; n=normalize(sn); t.xyz=normalize(st-n*dot(n,st));
}
Varyings ClusterSkinnedVert(Attributes input)
{
    uint objectIndex=_VisibleClusterIds[input.instanceID]>>16, vertexIndex; float3 p,n; float4 t; float2 uv;
    ApplyClusterSkinnedInstance(input.instanceID); FetchBaseVertex(input.vertexID,input.instanceID,p,n,t,uv,vertexIndex); SkinVertex(objectIndex,vertexIndex,p,n,t);
    VertexPositionInputs pos=GetVertexPositionInputs(p); VertexNormalInputs normal=GetVertexNormalInputs(n,t);
    Varyings o; o.positionCS=pos.positionCS; o.positionWS=pos.positionWS; o.normalWS=normal.normalWS; o.tangentWS=float4(normal.tangentWS,t.w); o.uv=TRANSFORM_TEX(uv,_BaseMap); o.clusterId=_VisibleClusterIds[input.instanceID]&0xffffu; return o;
}
half4 ClusterSkinnedFrag(Varyings i):SV_Target
{
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor; clip(albedo.a-_Cutoff);
    if (_EnableClusterColor > 0.5f) return half4(ClusterSkinnedDebugRgb(i.clusterId), albedo.a);
    float3 nts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale); float3 t=normalize(i.tangentWS.xyz),n=normalize(i.normalWS),b=cross(n,t)*i.tangentWS.w;
    InputData d=(InputData)0; d.positionWS=i.positionWS; d.normalWS=normalize(mul(nts,float3x3(t,b,n))); d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); d.shadowCoord=TransformWorldToShadowCoord(i.positionWS); d.bakedGI=SampleSH(d.normalWS);
    SurfaceData s=(SurfaceData)0; s.albedo=albedo.rgb;s.metallic=_Metallic;s.smoothness=_Smoothness;s.normalTS=nts;s.occlusion=1;s.alpha=albedo.a; return UniversalFragmentPBR(d,s);
}
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
#endif
