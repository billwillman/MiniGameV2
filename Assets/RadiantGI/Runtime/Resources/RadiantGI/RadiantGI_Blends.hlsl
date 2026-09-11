#ifndef RGI_BLENDS
#define RGI_BLENDS

    // Copyright 2022-2026 Kronnect - All Rights Reserved.
    
    TEXTURE2D_X(_CompareTexGI);
    float4 _CompareParams;
    float _DebugDepthMultiplier;
    float _DebugMotionVectorMultiplier;

    TEXTURE2D_X(_RadiantShadowMapRSM);

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareRenderingLayerTexture.hlsl"

    half4 FragCopyExact (VaryingsRGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);
        half4 pixel = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, i.uv);
        return pixel;
    }

    half4 FragCopy (VaryingsRGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        i.uv     = UnityStereoTransformScreenSpaceTex(i.uv);
        half4 pixel = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, i.uv);
        return pixel;
    }

    half4 FragAlbedo (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half3 albedo = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_LinearClamp, uv).rgb;
        return half4(albedo, 1.0);
    }

    half4 FragNormals (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half3 normals = GetWorldNormal(uv);
        return half4(normals, 1.0);
    }


    half4 FragDepth (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half depth = GetLinearEyeDownscaledDepth(uv);
        return half4(depth.xxx * _DebugDepthMultiplier / _ProjectionParams.z, 1.0);
    }

    float4 FragCopyDepth (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        float depth = GetRawDepth(uv);
        #if _TRANSPARENT_DEPTH_PREPASS
            float transparentDepth = SAMPLE_TEXTURE2D_X_LOD(_RadiantTransparentDepthTexture, sampler_PointClamp, uv, 0).r;
            #if UNITY_REVERSED_Z
                depth = max(depth, transparentDepth);
            #else
                depth = min(depth, transparentDepth);
            #endif
        #endif
        return float4(depth.xxx, 1.0);
    }

    half4 FragMotion (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half2 velocity = GetVelocity(uv);
        return half4(abs(velocity) * _DebugMotionVectorMultiplier, 0, 1.0);
    }

    half3 GetRenderingLayerDebugColor(uint bit) {
        uint palette = bit & 7u;
        if (palette == 0u) return half3(1.0, 0.1, 0.1);
        if (palette == 1u) return half3(0.1, 0.9, 0.2);
        if (palette == 2u) return half3(0.2, 0.45, 1.0);
        if (palette == 3u) return half3(1.0, 0.9, 0.1);
        if (palette == 4u) return half3(1.0, 0.15, 0.85);
        if (palette == 5u) return half3(0.1, 0.9, 1.0);
        if (palette == 6u) return half3(1.0, 0.45, 0.05);
        return half3(0.85, 0.85, 0.85);
    }

    half4 FragRenderingLayerMask (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        uint mask = LoadSceneRenderingLayer((uint2)input.positionCS.xy);
        if (mask == 0u) return half4(0, 0, 0, 1);

        half3 color = 0;
        half count = 0;
        UNITY_UNROLL
        for (uint bit = 0u; bit < 32u; bit++) {
            if ((mask & (1u << bit)) != 0u) {
                color += GetRenderingLayerDebugColor(bit);
                count += 1.0h;
            }
        }

        color /= max(count, 1.0h);
        return half4(saturate(color), 1.0);
    }

    half4 FragRSM (VaryingsRGI input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
        half4 rsm = SAMPLE_TEXTURE2D_X(_RadiantShadowMapRSM, sampler_LinearClamp, uv);
        return rsm;
    }


    half4 FragCompare (VaryingsRGI i) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 screenUV = i.uv;
        float2 pixelUV = UnityStereoTransformScreenSpaceTex(screenUV);
        float2 pixelNiceUV = pixelUV;

        // separator line + antialias
        float2 dd     = screenUV - 0.5.xx;
        float  co     = dot(_CompareParams.xy, dd);
        float  dist   = distance( _CompareParams.xy * co, dd );
        float4 aa     = saturate( (_CompareParams.w - dist) / abs(_MainTex_TexelSize.y) );

        float  sameSide = (_CompareParams.z > -5);
        float2 cp = float2(_CompareParams.y, -_CompareParams.x);
        float t = dot(dd, cp) > 0;

        UNITY_BRANCH
        if (sameSide) {
            float  sameSideHalfSpan = 0.5 * (abs(cp.x) + abs(cp.y));
            float  sameSidePanning = _CompareParams.z * 2.0 * sameSideHalfSpan;
            float2 sameSideOriginalUV = screenUV + cp * sameSidePanning;
            float2 sameSideGIUV = screenUV + cp * (sameSidePanning - sameSideHalfSpan);

            // Keep the current camera color where a diagonal displacement leaves the eye viewport.
            // Clipping before sampling removes clamp streaks without additional texture reads.
            float2 activeUV = lerp(sameSideOriginalUV, sameSideGIUV, t);
            clip(float4(activeUV, 1.0 - activeUV));

            pixelUV = UnityStereoTransformScreenSpaceTex(sameSideOriginalUV);
            pixelNiceUV = UnityStereoTransformScreenSpaceTex(sameSideGIUV);
        }

        float4 pixel  = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, pixelUV);
        float4 pixelNice = SAMPLE_TEXTURE2D_X(_CompareTexGI, sampler_PointClamp, pixelNiceUV);
        
        // are we on the beautified side?
        pixel         = lerp(pixel, pixelNice, t);
        return pixel + aa;
    }


#endif // RGI_BLENDS
