#ifndef RGI_CAPTURE_WORLDPOS
#define RGI_CAPTURE_WORLDPOS

    // Copyright 2022-2026 Kronnect - All Rights Reserved.

    struct AttributesFS {
        float4 positionHCS : POSITION;
        float2 uv          : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct VaryingsWorldPos {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    VaryingsWorldPos VertRGI(AttributesFS input) {
        VaryingsWorldPos output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = float4(input.positionHCS.xyz, 1.0);
        output.positionCS.y *= _ProjectionParams.x;
        output.uv = input.uv;
        return output;
    }

    float4 FragWorldPos(VaryingsWorldPos input) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

        float rawDepth = SampleSceneDepth(uv);

        // Match RadiantGI_Common.hlsl GetWorldPosition()
        #if UNITY_REVERSED_Z
            float depth = rawDepth;
        #else
            float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
        #endif

        float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        return float4(worldPos, 1.0);
    }


#endif // RGI_CAPTURE_WORLDPOS_
