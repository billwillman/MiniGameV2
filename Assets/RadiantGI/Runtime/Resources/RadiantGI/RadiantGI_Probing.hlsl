#ifndef RGI_PROBING
#define RGI_PROBING

    // Copyright 2022-2026 Kronnect - All Rights Reserved.
    
    // APV/Sky ambient sampling
    // Used by both Raycast (fallback) and Compose (subtraction)
    #define USES_APV (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))

    #if USES_APV
        #include "Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl"
    #endif

    #if UNITY_VERSION >= 600000
        #ifndef AMBIENT_PROBE_BUFFER
            #define AMBIENT_PROBE_BUFFER 0
        #endif
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"
    #endif
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    half3 SampleAmbientLighting(float3 posWS, half3 rayDirWS, float2 positionSS) {
        #if USES_APV
            half3 viewDirWS = normalize(posWS - _WorldSpaceCameraPos);
            half4 probeOcclusion;
            half3 bakeDiffuseLighting;
            EvaluateAdaptiveProbeVolume(posWS, rayDirWS, viewDirWS, positionSS, 0xFFFFFFFF, bakeDiffuseLighting, probeOcclusion);
            return bakeDiffuseLighting * probeOcclusion.rgb;
        #else
            half3 ambient = SampleSH(rayDirWS);
            // Apply time-of-day intensity when using realtime ambient probe
            half hasMainLight = dot(_MainLightColor.rgb, half3(1.0h, 1.0h, 1.0h));
            if (hasMainLight > 0.0h)
            {
                half mainLightY = _MainLightPosition.y;
                half timeOfDayIntensity = saturate(mainLightY * 1.5 + 0.2);
                ambient *= lerp(1, timeOfDayIntensity, REALTIME_AMBIENT_PROBE);
            }
            return ambient;
        #endif
    }

    // Traditional reflection probe sampling with box projection support
    half3 SampleProbe(TEXTURECUBE(_ProbeCube), SAMPLER(_SamplerCube), half4 probeHDRData, half3 rayDirWS, float3 positionWS, float4 probePosition, float4 boxMin, float4 boxMax) {
        half3 sampleVector = rayDirWS;
        #if _REFLECTION_PROBE_BOX_PROJECTION
        if (probePosition.w > 0.0) {
            sampleVector = BoxProjectedCubemapDirection(rayDirWS, positionWS, probePosition, boxMin, boxMax);
        }
        #endif
        
        half4 probeSample = SAMPLE_TEXTURECUBE_LOD(_ProbeCube, _SamplerCube, sampleVector, 0);
        probeSample.rgb = DecodeHDREnvironment(probeSample, probeHDRData);
        return probeSample.rgb;
    }

#if _FALLBACK_PROBE_ATLAS && USE_CLUSTER_LIGHT_LOOP
    // Samples reflection probes from atlas using cluster-based iteration (Forward+/Deferred+)
    // Adapted from CalculateIrradianceFromReflectionProbes from UniversalRP/ShaderLibrary/GlobalIllumination.hlsl
    // Returns both irradiance and weight for blending with APV/Sky ambient
    half3 SampleReflectionProbesWithWeight(half3 reflectVector, float3 positionWS, float2 normalizedScreenSpaceUV, out half outWeight)
    {
        half3 irradiance = half3(0.0h, 0.0h, 0.0h);
        half mip = 0.5; //PerceptualRoughnessToMipmapLevel(perceptualRoughness);
        float totalWeight = 0.0f;
        
        uint probeIndex;
        ClusterIterator it = ClusterInit(normalizedScreenSpaceUV, positionWS, 1);
        [loop] while (ClusterNext(it, probeIndex) && totalWeight < 0.99f)
        {
            probeIndex -= URP_FP_PROBES_BEGIN;
            float weight = CalculateProbeWeight(positionWS, urp_ReflProbes_BoxMin[probeIndex], urp_ReflProbes_BoxMax[probeIndex]);
            weight = min(weight, 1.0f - totalWeight);
            
            half3 sampleVector = reflectVector;
            if (_REFLECTION_PROBE_BOX_PROJECTION)
            {
                sampleVector = BoxProjectedCubemapDirection(reflectVector, positionWS, urp_ReflProbes_ProbePosition[probeIndex], urp_ReflProbes_BoxMin[probeIndex], urp_ReflProbes_BoxMax[probeIndex]);
            }
            
            uint maxMip = (uint)abs(urp_ReflProbes_ProbePosition[probeIndex].w) - 1;
            half probeMip = min(mip, maxMip);
            float2 uv = saturate(PackNormalOctQuadEncode(sampleVector) * 0.5 + 0.5);
            
            float mip0 = floor(probeMip);
            float mip1 = mip0 + 1;
            float mipBlend = probeMip - mip0;
            float4 scaleOffset0 = urp_ReflProbes_MipScaleOffset[probeIndex * 7 + (uint)mip0];
            float4 scaleOffset1 = urp_ReflProbes_MipScaleOffset[probeIndex * 7 + (uint)mip1];
            
            half3 irradiance0 = SAMPLE_TEXTURE2D_LOD(urp_ReflProbes_Atlas, sampler_LinearClamp, uv * scaleOffset0.xy + scaleOffset0.zw, 0.0).rgb;
            half3 irradiance1 = SAMPLE_TEXTURE2D_LOD(urp_ReflProbes_Atlas, sampler_LinearClamp, uv * scaleOffset1.xy + scaleOffset1.zw, 0.0).rgb;
            irradiance += weight * lerp(irradiance0, irradiance1, mipBlend);
            totalWeight += weight;
        }
        
        outWeight = saturate(totalWeight);
        return irradiance;
    }
#endif // _FALLBACK_PROBE_ATLAS

#endif // RGI_PROBING


