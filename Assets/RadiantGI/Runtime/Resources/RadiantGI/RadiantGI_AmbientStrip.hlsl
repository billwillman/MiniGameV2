#ifndef RGI_AMBIENT_STRIP
#define RGI_AMBIENT_STRIP

    // Copyright 2022-2026 Kronnect - All Rights Reserved.

    #include "RadiantGI_Probing.hlsl"

    half4 FragAmbientStrip (VaryingsRGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv = UnityStereoTransformScreenSpaceTex(i.uv);

        half4 sceneColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_PointClamp, uv);

        float rawDepth = GetRawDepth(uv);
        if (IsSkyBox(rawDepth)) return sceneColor;

        float3 wpos = GetWorldPosition(uv, rawDepth);
        half3 norm = GetWorldNormal(uv);
        half3 ambient = SampleAmbientLighting(wpos, norm, uv * SOURCE_SIZE);

        half3 ambientToSubtract;

        #if _FORWARD
            half3 hue = normalize(sceneColor.rgb + 0.01);
            ambientToSubtract = ambient * hue;
        #elif _FORWARD_AND_DEFERRED
            half occlusion;
            half3 brdfDiffuse;
            GetGBufferDiffuse(uv, brdfDiffuse, occlusion);
            if (all(brdfDiffuse == 0)) {
                half3 hue = normalize(sceneColor.rgb + 0.01);
                ambientToSubtract = ambient * hue;
            } else {
                ambientToSubtract = ambient * brdfDiffuse;
            }
        #else
            half3 brdfDiffuse;
            half occlusion;
            GetGBufferDiffuse(uv, brdfDiffuse, occlusion);
            ambientToSubtract = ambient * brdfDiffuse;
        #endif

        ambientToSubtract *= UNITY_AMBIENT_INTENSITY;
        sceneColor.rgb = max(sceneColor.rgb - ambientToSubtract, half3(0, 0, 0));

        return sceneColor;
    }

#endif // RGI_AMBIENT_STRIP
