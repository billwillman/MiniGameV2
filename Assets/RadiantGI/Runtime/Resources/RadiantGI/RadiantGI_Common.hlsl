#ifndef RGI_COMMON
#define RGI_COMMON

    // Copyright 2022-2026 Kronnect - All Rights Reserved.
    
    TEXTURE2D_X(_MainTex);
    float4 _MainTex_TexelSize;

    TEXTURE2D(_NoiseTex);
    float4 _NoiseTex_TexelSize;

	TEXTURE2D_X(_MotionVectorTexture);
    TEXTURE2D_X(_GBuffer0);
    TEXTURE2D_X(_GBuffer1);
    TEXTURE2D_X(_GBuffer2);

    TEXTURE2D_X(_DownscaledDepthRT);
    float4 _DownscaledDepthRT_TexelSize;

    #if _TRANSPARENT_DEPTH_PREPASS
        TEXTURE2D_X(_RadiantTransparentDepthTexture);
    #endif

    #define dot2(x) dot(x, x)
    
    float4x4 _WorldToViewDir;
    float4x4 _PrevInvViewProj;

	float4 _SourceSize;
	#define SOURCE_SIZE _SourceSize.xy
	#define GOLDEN_RATIO_ACUM _SourceSize.z
    #define FRAME_NUMBER _SourceSize.w

    float4 _IndirectData;
    #define INDIRECT_INTENSITY _IndirectData.x
    #define INDIRECT_MAX_BRIGHTNESS _IndirectData.y
    #define INDIRECT_DISTANCE_ATTENUATION _IndirectData.z
    #define RAY_REUSE_INTENSITY _IndirectData.w

    float4 _RayData;
    #define RAY_COUNT _RayData.x
    #define RAY_MAX_LENGTH _RayData.y
    #define RAY_MAX_SAMPLES _RayData.z
    #define THICKNESS _RayData.w

    float4 _TemporalData;
    #define TEMPORAL_BLEND _TemporalData.x
    #define TEMPORAL_RESPONSE_SPEED _TemporalData.y
    #define BLEND_FRAMES_COUNT _TemporalData.z
    #define CAMERA_TRANSLATION_FACTOR _TemporalData.w

    float4 _TemporalData2;
    #define DARK_THRESHOLD _TemporalData2.x
    #define DARK_THRESHOLD_MULTIPLIER _TemporalData2.y
    
    float4 _ExtraData;
    #define JITTER_AMOUNT _ExtraData.x
    #define BLUR_SPREAD _ExtraData.y
    #define NORMALS_INFLUENCE _ExtraData.z
    #define OCCLUSION_INTENSITY _ExtraData.w

    half4  _ExtraData2;
    #define LUMA_THRESHOLD _ExtraData2.x
    #define LUMA_MAX _ExtraData2.y
    #define COLOR_SATURATION _ExtraData2.z
    #define RSM_INTENSITY _ExtraData2.w

    half4  _ExtraData3;
    #define AO_INFLUENCE _ExtraData3.x
    #define ONE_MINUS_AO_INFLUENCE (1.0 - _ExtraData3.x)
    #define NEAR_FIELD_OBSCURANCE_SPREAD _ExtraData3.y
    #define NEAR_CAMERA_ATTENUATION _ExtraData3.z
    #define NEAR_FIELD_OBSCURANCE_INTENSITY _ExtraData3.w

    half4  _ExtraData4;
    #define NEAR_FIELD_OBSCURANCE_MAX_CAMERA_DISTANCE _ExtraData4.x
    #define NEAR_FIELD_OBSCURANCE_OCCLUDER_DISTANCE _ExtraData4.y
    #define UNITY_AMBIENT_INTENSITY _ExtraData4.z
    #define REALTIME_AMBIENT_PROBE _ExtraData4.w

    float _LegacyAmbientSubtract;

    float4 _BoundsXZ;

    half3 _NFOTint;
    #define NEAR_FIELD_OBSCURANCE_TINT _NFOTint

    half4 _ExtraData5;
    #define DOWNSAMPLING _ExtraData5.x
    #define SOURCE_BRIGHTNESS _ExtraData5.y
    #define GI_WEIGHT _ExtraData5.z
    #define FALLBACK_APV_SKY _ExtraData5.w

	struct AttributesFS {
		float4 positionHCS : POSITION;
		float2 uv          : TEXCOORD0;
		UNITY_VERTEX_INPUT_INSTANCE_ID
	};

 	struct VaryingsRGI {
    	float4 positionCS : SV_POSITION;
    	float2 uv  : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
	};


	VaryingsRGI VertRGI(AttributesFS input) {
	    VaryingsRGI output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = float4(input.positionHCS.xyz, 1.0);
        output.positionCS.y *= _ProjectionParams.x;

        output.uv = input.uv;
    	return output;
	}

    float GetRawDepth(float2 uv) {
        float depth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_PointClamp, uv, 0).r;
        return depth;
    }

    float RawToLinearEyeDepth(float rawDepth) {
        #if _ORTHO_SUPPORT
            #if UNITY_REVERSED_Z
                float orthoRawDepth = 1.0 - rawDepth;
            #else
                float orthoRawDepth = rawDepth;
            #endif
            float eyeDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, orthoRawDepth);
        #else
            float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
        #endif
        return eyeDepth;
    }

    float LinearEyeDepthToRaw(float eyeDepth) {
        // Inverse of LinearEyeDepth: raw = (1/eyeDepth - _ZBufferParams.w) / _ZBufferParams.z
        float rawDepth = (1.0 / eyeDepth - _ZBufferParams.w) / _ZBufferParams.z;
        #if _ORTHO_SUPPORT
            rawDepth = (eyeDepth - _ProjectionParams.y) / (_ProjectionParams.z - _ProjectionParams.y);
            #if UNITY_REVERSED_Z
                rawDepth = 1.0 - rawDepth;
            #endif
        #endif
        return rawDepth;
    }

    float GetLinearEyeDepth(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return RawToLinearEyeDepth(rawDepth);
    }

    float GetDownscaledRawDepth(float2 uv) {
        float depth = SAMPLE_TEXTURE2D_X_LOD(_DownscaledDepthRT, sampler_PointClamp, uv, 0).r;
        return depth;
    }

    float GetLinearEyeDownscaledDepth(float2 uv) {
        float rawDepth = GetDownscaledRawDepth(uv);
        return RawToLinearEyeDepth(rawDepth);
    }

    float3 GetViewSpacePosition(float2 uv, float rawDepth) {
        #if UNITY_REVERSED_Z
            float depth = 1.0 - rawDepth;
        #else
            float depth = rawDepth;
        #endif
        depth = 2.0 * depth - 1.0;
        #if defined(USING_STEREO_MATRICES)
            float3 rayStart = ComputeViewSpacePosition(uv, depth, unity_StereoCameraInvProjection[unity_StereoEyeIndex]);
        #else
            float3 rayStart = ComputeViewSpacePosition(uv, depth, unity_CameraInvProjection);
        #endif
        return rayStart;
    }

    float3 GetViewSpacePosition(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return GetViewSpacePosition(uv, rawDepth);
    }

    float3 GetWorldPosition(float2 uv, float rawDepth) {
        
         #if UNITY_REVERSED_Z
              float depth = rawDepth;
         #else
              float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
         #endif
         float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        return worldPos;
    }

    float4x4 _PrevInvViewProjStereo[2];

    float3 GetPrevWorldPosition(float2 uv, float rawDepth) {

         #if UNITY_REVERSED_Z
              float depth = rawDepth;
         #else
              float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
         #endif
         #if defined(USING_STEREO_MATRICES)
            float3 worldPos = ComputeWorldSpacePosition(uv, depth, _PrevInvViewProjStereo[unity_StereoEyeIndex]);
         #else
            float3 worldPos = ComputeWorldSpacePosition(uv, depth, _PrevInvViewProjStereo[0]);
         #endif
        return worldPos;
    }

    float3 GetWorldPosition(float2 uv) {
        float rawDepth = GetRawDepth(uv);
        return GetWorldPosition(uv, rawDepth);
    }


	half3 GetWorldNormal(uint2 uv) {
		half3 norm = LoadSceneNormals(uv);
		return norm;
	}

	half3 GetWorldNormal(float2 uv) {
		half3 norm = SampleSceneNormals(uv);
		return norm;
	}

    float4x4 _RadiantCurrVP;
    float4x4 _RadiantPrevVP;
    float _RadiantEditorMV;

    half2 GetVelocity(float2 uv) {
        // Scene view camera motion vectors fallback
        UNITY_BRANCH
        if (_RadiantEditorMV > 0.5) {
            float3 posWS = GetWorldPosition(uv);
            float4 currCS = mul(_RadiantCurrVP, float4(posWS, 1.0));
            float4 prevCS = mul(_RadiantPrevVP, float4(posWS, 1.0));
            float2 currNDC = currCS.xy / currCS.w;
            float2 prevNDC = prevCS.xy / prevCS.w;
            float2 v = currNDC - prevNDC;
        #if UNITY_UV_STARTS_AT_TOP
            v.y = -v.y;
        #endif
            return (half2)(v * 0.5);
        }
		half2 mv = SAMPLE_TEXTURE2D_X_LOD(_MotionVectorTexture, sampler_PointClamp, uv, 0).xy;
        return mv;
    }

    half GetMotionAtten(half2 velocity) {
        half2 absVel = abs(velocity) * _ScreenParams.xy;
        half maxPixelMove = max(absVel.x, absVel.y);
        maxPixelMove = max(maxPixelMove, CAMERA_TRANSLATION_FACTOR);
        half motionAtten = TEMPORAL_BLEND * saturate(1.0 - TEMPORAL_RESPONSE_SPEED * maxPixelMove);
        return motionAtten;
    }


    half GetLuma(half3 rgb) {
        const half3 lum = half3(0.299h, 0.587h, 0.114h);
        return dot(rgb, lum);
    }

    // from URP lighting library
    #ifndef kDieletricSpec
        #define kDieletricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)
    #endif

    #ifndef kMaterialFlagSpecularSetup
        #define kMaterialFlagSpecularSetup 4
    #endif

    void GetGBufferData(float2 uv, out half3 albedo, out half occlusion, out half metallic) {
        half4 pixelGBuffer0 = SAMPLE_TEXTURE2D_X(_GBuffer0, sampler_PointClamp, uv);
        albedo = pixelGBuffer0.rgb;

        half4 pixelGBuffer1 = SAMPLE_TEXTURE2D_X(_GBuffer1, sampler_PointClamp, uv);
        occlusion = pixelGBuffer1.a;

        // Extract metallic - handles both metallic and specular workflow
        metallic = (pixelGBuffer0.a == kMaterialFlagSpecularSetup) ? 
            (1.0 - max(max(pixelGBuffer1.r, pixelGBuffer1.g), pixelGBuffer1.b)) : 
            pixelGBuffer1.r;
    }

    void GetGBufferDiffuse(float2 uv, out half3 brdfDiffuse, out half occlusion) {
        half3 albedo;
        half metallic;
        GetGBufferData(uv, albedo, occlusion, metallic);
        half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
        brdfDiffuse = albedo * oneMinusReflectivity;
    }

    bool IsSkyBox(float rawDepth) {
        #if UNITY_REVERSED_Z
            return rawDepth <= 0;
		#else
            return rawDepth >= 1.0;
		#endif
    }

    bool IsOutsideBounds(float3 wpos) {
        return any(wpos.xz < _BoundsXZ.xy) || any(wpos.xz > _BoundsXZ.zw);
    }

#endif // RGI_COMMON