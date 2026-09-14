#ifndef RGI_ORGANIC_LIGHTS
#define RGI_ORGANIC_LIGHTS

	// Copyright 2022-2026 Kronnect - All Rights Reserved.

half4 _OrganicLightData;
half3 _OrganicLightTint;
float3 _OrganicLightOffset;

#define SPREAD _OrganicLightData.x
#define INTENSITY _OrganicLightData.y
#define THRESHOLD _OrganicLightData.z
#define NORMALS_STRENGTH _OrganicLightData.w

	half ComputeLight(float3 wpos, float3 normalWS, float scale) {
		wpos -= _OrganicLightOffset;
		float3 hazeUV = wpos * (SPREAD * scale);
		half n1 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_LinearRepeat, hazeUV.zy).r;
		half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_LinearRepeat, hazeUV.xz).r;
		half n3 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_LinearRepeat, hazeUV.xy).r;
		float3 triW = abs(normalWS);
		float3 weights = triW / (triW.x + triW.y + triW.z);
		half haze = dot(half3(n1, n2, n3), weights);
		return haze;
	}

	half4 FragOrganicLight(VaryingsRGI i) : SV_Target {

        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
		i.uv = UnityStereoTransformScreenSpaceTex(i.uv);

		float rawDepth = GetRawDepth(i.uv);

		 // exclude skybox
		if (IsSkyBox(rawDepth)) return 0;

		float3 normalWS = GetWorldNormal(i.uv);

		float3 wpos = GetWorldPosition(i.uv, rawDepth);

		half haze = ComputeLight(wpos, normalWS, 1.0);

		#if _DISTANCE_BLENDING
			half haze2 = ComputeLight(wpos, normalWS, 0.133);
			float depth01 = Linear01Depth(rawDepth, _ZBufferParams);
			depth01 = sqrt(depth01);
			haze = lerp(haze, haze2, depth01);
		#endif

		haze = saturate( (haze - THRESHOLD) * INTENSITY);

		// reduce fog effect
        float3 cameraPosition = GetCameraPositionWS();
        half3 toCamera = normalize(cameraPosition - wpos);
        half ndot = max(0, 1 + dot(normalWS, toCamera) * NORMALS_STRENGTH - NORMALS_STRENGTH);
        haze *= ndot;

		return half4(_OrganicLightTint * haze, 0);
	}

#endif // RGI_ORGANIC_LIGHT_PASS
