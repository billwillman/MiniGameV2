#ifndef RGI_CAPTURE_NORMALS
#define RGI_CAPTURE_NORMALS

	// Copyright 2022-2026 Kronnect - All Rights Reserved.

    TEXTURE2D_X(_RadiantShadowMapWorldPos);
    SAMPLER(sampler_PointClamp_RSM);
    float4 _RadiantShadowMapWorldPos_TexelSize;

	struct AttributesFS {
		float4 positionHCS : POSITION;
		float2 uv          : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
	};

 	struct VaryingsRGI {
    	float4 positionCS : SV_POSITION;
    	float2 uv         : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
	};


	VaryingsRGI VertRGI(AttributesFS input) {
	    VaryingsRGI output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = float4(input.positionHCS.xyz, 1.0);
        output.positionCS.y *= _ProjectionParams.x;
        output.uv = input.uv;
    	return output;
	}


    float4 FragNormals(VaryingsRGI input) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 uv0 = input.uv;
        float2 uv1 = uv0 + float2(_RadiantShadowMapWorldPos_TexelSize.x, 0);
        float2 uv2 = uv0 + float2(0, _RadiantShadowMapWorldPos_TexelSize.y);

        float3 wpos0 = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapWorldPos, sampler_PointClamp_RSM, uv0, 0).xyz;
        float3 wpos1 = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapWorldPos, sampler_PointClamp_RSM, uv1, 0).xyz;
        float3 wpos2 = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapWorldPos, sampler_PointClamp_RSM, uv2, 0).xyz;

        float3 v1 = normalize(wpos2 - wpos0);
        float3 v2 = normalize(wpos1 - wpos0);

        float3 normal = normalize(cross(v1, v2));

        return float4(normal, 1.0);
    }


#endif // RGI_CAPTURE_NORMALS_
