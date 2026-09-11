#ifndef RGI_TACUM
#define RGI_TACUM

    // Copyright 2022-2026 Kronnect - All Rights Reserved.

    TEXTURE2D_X(_RadiantPrevResolve);
    TEXTURE2D_X(_RadiantRayBufferRT);
    TEXTURE2D_X(_RadiantRayConfidenceRT);

    #define TEMPORAL_DEPTH_REJECTION 0.5

    inline bool any_invalid_h4(half4 v) {
        return any((v != v) | (abs(v) > 1e5h));
    }

    inline float3 EyeDepthToWorldPos(float2 uv, float eyeDepth) {
        return GetWorldPosition(uv, LinearEyeDepthToRaw(eyeDepth));
    }

    inline float3 EyeDepthToPrevWorldPos(float2 uv, float eyeDepth) {
        return GetPrevWorldPosition(uv, LinearEyeDepthToRaw(eyeDepth));
    }

    inline void GetNeighborhoodBox(float2 uv, out half3 boxMin, out half3 boxMax) {
        float2 texel = _MainTex_TexelSize.xy;
        half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv).rgb;
        half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(0, texel.y)).rgb;
        half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv - float2(0, texel.y)).rgb;
        half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv - float2(texel.x, 0)).rgb;
        half3 c4 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv + float2(texel.x, 0)).rgb;
        half3 t0 = min(c0, c1);
        half3 t1 = min(c2, c3);
        half3 t2 = min(t0, t1);
        boxMin = min(t2, c4);
        t0 = max(c0, c1);
        t1 = max(c2, c3);
        t2 = max(t0, t1);
        boxMax = max(t2, c4);
    }

    half4 FragRGI(VaryingsRGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

        half4 newData = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
        if (any_invalid_h4(newData) || newData.w == 0) return 0;

        half2 velocity = GetVelocity(uv);
        float2 prevUV = uv - velocity;
        bool2 oob = (prevUV < 0.0) | (prevUV >= 1.0);
        if (any(oob)) return newData;

        half4 prevData = SAMPLE_TEXTURE2D_X(_RadiantPrevResolve, sampler_PointClamp, prevUV);
        if (any_invalid_h4(prevData) || prevData.w == 0) return newData;

        // motion factor
        half wMotion = GetMotionAtten(velocity);

        // color clamping based on neighborhood
        half3 boxMin, boxMax;
        GetNeighborhoodBox(uv, boxMin, boxMax);
        prevData.rgb = clamp(prevData.rgb, boxMin, boxMax);

        // blend previous and current frame
        half3 blended = lerp(newData.rgb, prevData.rgb, wMotion);
        return half4(max(0, blended), newData.w);
    }

    void FragRGI2(VaryingsRGI i, out half4 outColor : SV_Target0, out half outConfidence : SV_Target1) {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

        half4 newData = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);
        if (any_invalid_h4(newData)) {
            outColor = 0;
            outConfidence = 0;
            return;
        }
        if (newData.w == 0) {
            outColor = newData;
            outConfidence = 0;
            return;
        }

        half2 velocity = GetVelocity(uv);
        float2 prevUV = uv - velocity;
        bool2 oob = (prevUV < 0.0) | (prevUV >= 1.0);
        if (any(oob)) {
            outColor = newData;
            outConfidence = 1;
            return;
        }

        half prevConfidence = SAMPLE_TEXTURE2D_X(_RadiantRayConfidenceRT, sampler_LinearClamp, prevUV).r;
        if (prevConfidence <= 0) {
            outColor = newData;
            outConfidence = 1;
            return;
        }

        half4 prevData = SAMPLE_TEXTURE2D_X(_RadiantRayBufferRT, sampler_LinearClamp, prevUV);
        if (any_invalid_h4(prevData) | (prevData.w == 0)) {
            outColor = newData;
            outConfidence = 1;
            return;
        }

        // disocclusion check with adaptive depth threshold
        float3 currWorldPos = EyeDepthToWorldPos(uv, newData.w);
        float3 prevWorldPos = EyeDepthToPrevWorldPos(prevUV, prevData.w);
        float3 posDiff = currWorldPos - prevWorldPos;
        float distSq = dot(posDiff, posDiff);
        float depthFootprint = newData.w * 0.01;
        float adaptiveThreshold = max(TEMPORAL_DEPTH_REJECTION, depthFootprint);
        float thresholdSq = adaptiveThreshold * adaptiveThreshold;
        bool isValid = distSq < thresholdSq;
        
        half confidence;
        half motionFactor = 1.0 - GetMotionAtten(velocity);
        
        if (isValid) {
            half3 avgColor = (newData.rgb + prevData.rgb) * 0.5;
            half brightness = GetLuma(avgColor);
            half darkThreshold = brightness < DARK_THRESHOLD ? DARK_THRESHOLD_MULTIPLIER * (1.0 - brightness / DARK_THRESHOLD) : 0.0;
            confidence = min(prevConfidence + 1.0h, BLEND_FRAMES_COUNT + darkThreshold) ;
        } else {
            half3 boxMin, boxMax;
            GetNeighborhoodBox(uv, boxMin, boxMax);
            prevData.rgb = clamp(prevData.rgb, boxMin, boxMax);
            confidence = 1;
            prevConfidence = 1;
        }

        half blendIntensity = prevConfidence * rcp(prevConfidence + 0.5 + motionFactor + CAMERA_TRANSLATION_FACTOR);
        outColor.rgb = lerp(newData.rgb, prevData.rgb, blendIntensity);
        outColor.rgb = max(0, outColor.rgb);
        outColor.w = newData.w;

        outConfidence = confidence;
    }

#endif // RGI_TACUM

