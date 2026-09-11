#ifndef RGI_RAYCAST
#define RGI_RAYCAST

	// Copyright 2022-2026 Kronnect - All Rights Reserved.

	#include "RadiantGI_Probing.hlsl"

	TEXTURE2D_X(_RadiantPrevResolve);
	TEXTURE2D_X(_RadiantRSMBuffer);

	TEXTURECUBE(_Probe1Cube);
	TEXTURECUBE(_Probe2Cube);
	SAMPLER(sampler_LinearRepeat_Cube1);
	SAMPLER(sampler_LinearRepeat_Cube2);
	half3 _ProbeData;
	half4 _Probe1HDR, _Probe2HDR;
	float4 _Probe1BoxMin, _Probe1BoxMax, _Probe1ProbePosition;
	float4 _Probe2BoxMin, _Probe2BoxMax, _Probe2ProbePosition;
	half3 _FallbackDefaultAmbient;

	struct VaryingsRaycast {
		float4 positionCS : SV_POSITION;
		float4 uv  : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
		UNITY_VERTEX_OUTPUT_STEREO
	};

	VaryingsRaycast VertRaycast(AttributesFS input) {
		VaryingsRaycast output;
		UNITY_SETUP_INSTANCE_ID(input);
		UNITY_TRANSFER_INSTANCE_ID(input, output);
		UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
		output.positionCS = float4(input.positionHCS.xyz, 1.0);

		#if UNITY_UV_STARTS_AT_TOP
			output.positionCS.y *= -1;
		#endif

		output.uv.xy = input.uv;
		float4 projPos = output.positionCS * 0.5;
		projPos.xy = projPos.xy + projPos.w;
		output.uv.zw = projPos.xy;
		return output;
	}

	float k0, q0;
	float4x4 proj;
	void PrepareRay(float2 uv, float3 rayStart) {
		#if defined(USING_STEREO_MATRICES)
			proj = unity_StereoCameraProjection[unity_StereoEyeIndex];
		#else
			proj = unity_CameraProjection;
		#endif
		#if _ORTHO_SUPPORT
			float4 p0 = float4(uv, rayStart.z, 1.0);
			k0 = 1.0;
			q0 = rayStart.z;
		#else
			proj._13 *= -1; // fix for lens shift
			proj._23 *= -1;
			float4 sposStart = mul(proj, float4(rayStart, 1.0));
			k0 = rcp(sposStart.w);
			q0 = rayStart.z * k0;
		#endif
	}

	half4 Raycast(float2 uv, float3 rayStart, float3 rayDir, float jitterOffset) {

		float  rayLength = RAY_MAX_LENGTH;
        float3 rayEnd = rayStart + rayDir * rayLength;
        if (rayEnd.z < 1) {
            rayLength = abs ((1 - rayStart.z) / rayDir.z);
            rayEnd = rayStart + rayDir * rayLength;
        }

		float4 sposEnd = mul(proj, float4(rayEnd, 1.0));

		#if _ORTHO_SUPPORT
		    float2 uv1 = (sposEnd.xy + 1.0) * 0.5;
            float4 p0 = float4(uv, rayStart.z, 1.0);
            float4 p1 = float4(uv1, rayEnd.z, 1.0);
		#else
			float k1 = rcp(sposEnd.w);
			float q1 = rayEnd.z * k1;
			float2 uv1 = sposEnd.xy * rcp(rayEnd.z) * 0.5 + 0.5;
			float4 p0 = float4(uv, q0, k0);
			float4 p1 = float4(uv1, q1, k1);
		#endif

		float2 duv = uv1 - uv;
		float2 duvPixel = abs(duv * _DownscaledDepthRT_TexelSize.zw);
		float pixelDistance = max(duvPixel.x, duvPixel.y);
		pixelDistance = max(1, pixelDistance);

		int sampleCount = (int)min(pixelDistance, RAY_MAX_SAMPLES);
		float rcpSampleCount = rcp((float)sampleCount);
		float rcpPixelDist = rcp(pixelDistance);

		float4 p = p0;
		float jitter = jitterOffset * rcpSampleCount;

		bool hit = false;
		float prevDepthDiff = 0; // signed; tracks crossings between samples for thin-geometry hits
		float sceneDepth = 0;
		for (int k = 1; k <= sampleCount && !hit; k++) {
			float progress = k * rcpSampleCount;
			float t = progress * progress;
			t = max(t, k * rcpPixelDist);
            t = t + jitter;
			p = lerp(p0, p1, t);
            if (any(p.xy < 0 | p.xy >= 1)) return 0;
			sceneDepth = GetLinearEyeDownscaledDepth(p.xy);
			#if _ORTHO_SUPPORT
				float pz = p.z;
			#else
				float pz = p.z * rcp(p.w);
			#endif
			float depthDiff = pz - sceneDepth;
			hit = depthDiff > 0.02 && (depthDiff < THICKNESS || prevDepthDiff < 0);
			prevDepthDiff = depthDiff;
		}

		if (hit) {
			float zdist = (sceneDepth - rayStart.z) / (0.0001 + rayEnd.z - rayStart.z);
			if (zdist > 1) return 0; // out of max distance; double check because prevDepthDiff could be < 0 and trigger a hit beyond max distance

			zdist *= rayLength;

			// quadratic distance attenuation
			half distSqr = zdist * zdist;
			half distAtten = rcp(1.0 + distSqr);

			// indirect term - apply motion compensation when extra bounce is enabled
			float2 hitSampleUV = p.xy;
			#if _ONE_EXTRA_BOUNCE
				float2 hitVelocity = GetVelocity(p.xy);
				float2 prevUV = p.xy - hitVelocity;
				hitSampleUV = all(prevUV >= 0.0) && all(prevUV < 1.0) ? prevUV : p.xy;
			#endif
			half3 indirect = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_PointClamp, hitSampleUV, 0).rgb; // point clamp to avoid color bleed
			indirect = clamp(indirect, 0, 32); // keep source data under reasonable range and avoid NaN
			half invDistSqrWeight = lerp(1.0, distAtten, INDIRECT_DISTANCE_ATTENUATION);
			indirect *= invDistSqrWeight;

			return half4(indirect, 1.0);
		}

		return 0; // miss
	}


	float3 GetTangent(float3 v) {
		return abs(v.x) > abs(v.z) ? float3(-v.y, v.x, 0.0) : float3(0.0, -v.z, v.y);
	}

	// Cosine-weighted hemisphere sample using precomputed TBN and sphere sin/cos
	float3 GetJitteredNormal(float u, float s, float c, float3 norm, float3 tangent, float3 bitangent) {
		float omu = 1.0f - u;
		float z = omu * rsqrt(max(omu, 1e-8));
		float r = u * rsqrt(max(u, 1e-8));
		return tangent * (c * r) + bitangent * (s * r) + norm * z;
	}

	float AnimateNoise(float3 magic, float2 pixCoord, int frameCount) {
		float2 frameMagicScale = float2(2.083f, 4.867f);
		pixCoord += frameCount * frameMagicScale;
		return frac(magic.z * 52 * frac(dot(pixCoord, magic.xy)));
	}
	
	half4 FragRaycast (VaryingsRaycast input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv.xy);
        
        //float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.uv.xy).r;
        float rawDepth = GetDownscaledRawDepth(input.uv.xy);  // faster

         // exclude skybox
        if (IsSkyBox(rawDepth)) return 0;

        float3 rayStart = GetViewSpacePosition(input.uv.zw, rawDepth);
        float2 pos = uv * SOURCE_SIZE;
        float3 normalWS = GetWorldNormal((uint2)pos);
        float3 normalVS = mul((float3x3)_WorldToViewDir, normalWS);
        normalVS.z *= -1.0;

        // Precompute TBN in view space
        float3 tangentVS = normalize(GetTangent(normalVS));
        float3 bitangentVS = cross(tangentVS, normalVS);

        half4 indirect = 0;

        // More uniform noise
        float2 noisePos = pos / DOWNSAMPLING;
        float3 noises = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_PointRepeat, noisePos * _NoiseTex_TexelSize.xy, 0).xyz;
        noises.z = AnimateNoise(noises, noisePos, FRAME_NUMBER);

        float jitterOffset = noises.y * JITTER_AMOUNT;

        PrepareRay(uv, rayStart);

        // Reuse sincos across rays
        float sSC, cSC;
        sincos(2.0f * PI * noises.z, sSC, cSC);

        float3 wpos = GetWorldPosition(uv, rawDepth);
        half totalProbeWeight = 0;

        float goldenRatioAcum = GOLDEN_RATIO_ACUM;
        #if _USES_MULTIPLE_RAYS
            for (int k=0; k<RAY_COUNT; k++) {
                float u = frac(noises.x + goldenRatioAcum);
                float3 rayDirVS = GetJitteredNormal(u, sSC, cSC, normalVS, tangentVS, bitangentVS);
                half4 indirectSample = Raycast(uv, rayStart, rayDirVS, jitterOffset);
               
                if (indirectSample.w == 0) { // add probe contribution
                    half probeWeight = 0;
                    float3 rayDirWS = mul((float3x3)UNITY_MATRIX_I_V, rayDirVS);
                    
                    #if _FALLBACK_PROBE_ATLAS && USE_CLUSTER_LIGHT_LOOP
                        indirectSample.rgb = SampleReflectionProbesWithWeight(rayDirWS, wpos, uv, probeWeight) * _ProbeData.z;
                        probeWeight *= _ProbeData.z;
                    #elif (_FALLBACK_1_PROBE || _FALLBACK_2_PROBES)
                        indirectSample.rgb = SampleProbe(_Probe1Cube, sampler_LinearRepeat_Cube1, _Probe1HDR, rayDirWS, wpos, _Probe1ProbePosition, _Probe1BoxMin, _Probe1BoxMax) * _ProbeData.x;
                        probeWeight = _ProbeData.x;
                        #if _FALLBACK_2_PROBES
                            indirectSample.rgb += SampleProbe(_Probe2Cube, sampler_LinearRepeat_Cube2, _Probe2HDR, rayDirWS, wpos, _Probe2ProbePosition, _Probe2BoxMin, _Probe2BoxMax) * _ProbeData.y;
                            probeWeight += _ProbeData.y;
                        #endif
                    #endif
                    totalProbeWeight += probeWeight;
                }

                goldenRatioAcum += 0.618033989f; // GOLDEN_RATIO;
                indirect.rgb += indirectSample.rgb;
                indirect.w += indirectSample.w;
            }
            // Average results across multiple rays
			float rcpRaysCount = rcp((float)RAY_COUNT);
            indirect.rgb *= rcpRaysCount;
            totalProbeWeight *= rcpRaysCount;
        #else
            float u = frac(noises.x + goldenRatioAcum);
            float3 rayDirVS = GetJitteredNormal(u, sSC, cSC, normalVS, tangentVS, bitangentVS);
            indirect = Raycast(uv, rayStart, rayDirVS, jitterOffset);
            
            if (indirect.w == 0) {
                float3 rayDirWS = mul((float3x3)UNITY_MATRIX_I_V, rayDirVS);
                #if _FALLBACK_PROBE_ATLAS && USE_CLUSTER_LIGHT_LOOP
                    indirect.rgb = SampleReflectionProbesWithWeight(rayDirWS, wpos, uv, totalProbeWeight) * _ProbeData.z;
                    totalProbeWeight *= _ProbeData.z;
                #elif (_FALLBACK_1_PROBE || _FALLBACK_2_PROBES)
                    indirect.rgb = SampleProbe(_Probe1Cube, sampler_LinearRepeat_Cube1, _Probe1HDR, rayDirWS, wpos, _Probe1ProbePosition, _Probe1BoxMin, _Probe1BoxMax) * _ProbeData.x;
                    totalProbeWeight = _ProbeData.x;
                    #if _FALLBACK_2_PROBES
                        indirect.rgb += SampleProbe(_Probe2Cube, sampler_LinearRepeat_Cube2, _Probe2HDR, rayDirWS, wpos, _Probe2ProbePosition, _Probe2BoxMin, _Probe2BoxMax) * _ProbeData.y;
                        totalProbeWeight += _ProbeData.y;
                    #endif
                #endif
            }
        #endif

        // Fill remaining weight with APV/Sky + RSM + Feedback from previous frames + minimum ambient
        half remainingWeight = saturate(1.0 - totalProbeWeight);
        UNITY_BRANCH
        if (remainingWeight > 0.01) {
            half3 remainingLight = SampleAmbientLighting(wpos, normalWS, uv * SOURCE_SIZE) * FALLBACK_APV_SKY;
            
            #if _FALLBACK_RSM
                half3 rsmLight = SAMPLE_TEXTURE2D_X_LOD(_RadiantRSMBuffer, sampler_LinearClamp, uv, 0).rgb;
                remainingLight += rsmLight;
            #endif


            #if _REUSE_RAYS
                float2 velocity = GetVelocity(uv);
                float2 prevUV = uv - velocity;
                if (all(prevUV >= 0.0 && prevUV <= 1.0)) {
                    half4 prevResolve = SAMPLE_TEXTURE2D_X_LOD(_RadiantPrevResolve, sampler_PointClamp, prevUV, 0);
                    float currEyeDepth = RawToLinearEyeDepth(rawDepth);
                    float prevEyeDepth = prevResolve.w;
                    float depthThreshold = max(0.5, currEyeDepth * 0.05);
                    if (prevEyeDepth > 0 && abs(currEyeDepth - prevEyeDepth) < depthThreshold) {
                        half wMotion = GetMotionAtten(velocity);
                        half w = saturate(RAY_REUSE_INTENSITY * wMotion);
                        remainingLight = lerp(remainingLight, prevResolve.rgb, w);
                    }
                }
            #endif
            
            remainingLight = max(remainingLight, _FallbackDefaultAmbient);
            indirect.rgb += remainingLight * remainingWeight;
        }

        half luma = GetLuma(indirect.rgb);
        half lumaClamped = saturate(INDIRECT_MAX_BRIGHTNESS * rcp(luma + 0.001h));
        indirect.rgb *= lumaClamped;
        indirect.rgb *= (half)(luma > LUMA_THRESHOLD);

        half eyeDepth = RawToLinearEyeDepth(rawDepth);
        indirect.w = eyeDepth;

        return indirect;
    }

#endif // RGI_RAYCAST
