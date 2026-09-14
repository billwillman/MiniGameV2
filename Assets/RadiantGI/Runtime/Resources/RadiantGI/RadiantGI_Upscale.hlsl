#ifndef RGI_UPSCALE
#define RGI_UPSCALE

	// Copyright 2022-2026 Kronnect - All Rights Reserved.

    TEXTURE2D_X(_InputRTGI);
    TEXTURE2D_X(_NFO_RT);

    #include "RadiantGI_Probing.hlsl"

    // _VIRTUAL_EMITTERS_LAYERS implies emitters are active; treat both keywords as enabling the emitter path.
    #if defined(_VIRTUAL_EMITTERS) || defined(_VIRTUAL_EMITTERS_LAYERS)
        #define _RGI_EMITTERS_ON 1
    #endif

    #if _RGI_EMITTERS_ON

        #define MAX_EMITTERS 32

        CBUFFER_START(RadiantGIEmittersBuffer)
            float4 _EmittersBoxMin[MAX_EMITTERS];
            float4 _EmittersBoxMax[MAX_EMITTERS];
            float4 _EmittersShapeData[MAX_EMITTERS];
            float4 _EmittersRotation[MAX_EMITTERS];   // inverse rotation (world->local), unit quaternion
            float4 _EmittersLayerMasks[MAX_EMITTERS];  // xy = 32-bit rendering layer mask split into 16-bit chunks
            float3 _EmittersPositions[MAX_EMITTERS];
            half3 _EmittersColors[MAX_EMITTERS];
            int _EmittersCount;
        CBUFFER_END

        #if _VIRTUAL_EMITTERS_LAYERS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareRenderingLayerTexture.hlsl"
        #endif

        // Rotates v by quaternion q (xyz=imag, w=real). Identity (0,0,0,1) leaves v unchanged.
        float3 RadiantQuatRotate(float4 q, float3 v) {
            return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
        }

        half3 GetVirtualEmitters(float3 wpos, half3 norm, uint2 pixelCoord) {
            half3 sum = 0;

            #if _VIRTUAL_EMITTERS_LAYERS
                uint pixelLayers = LoadSceneRenderingLayer(pixelCoord);
            #endif

            UNITY_LOOP
            for (int k = 0; k < _EmittersCount; k++) {
                float4 boxMin = _EmittersBoxMin[k];
                float4 boxMax = _EmittersBoxMax[k];

                #if _VIRTUAL_EMITTERS_LAYERS
                    float4 layerMaskData = _EmittersLayerMasks[k];
                    uint emitterLayerMask = ((uint)layerMaskData.y << 16) | (uint)layerMaskData.x;
                    if ((pixelLayers & emitterLayerMask) == 0) continue;
                #endif

                // AABB area-of-influence cull as box-SDF: scalar branch, no short-circuit.
                float3 outside = max(boxMin.xyz - wpos, wpos - boxMax.xyz);
                if (max(outside.x, max(outside.y, outside.z)) > 0) continue;

                float3 emitterPos = _EmittersPositions[k];
                float3 shapeExt   = _EmittersShapeData[k].xyz;     // (0,0,0) for point emitters

                // Closest point on the (possibly rotated) box: enter local space via inverse rotation,
                // clamp to ±shapeExt, stay there. Rotation preserves distances so d2source matches the
                // world-space closest-point distance. For points, shapeExt = 0 -> clamp collapses to 0.
                float3 localOffset = RadiantQuatRotate(_EmittersRotation[k], wpos - emitterPos);
                float3 localSource = clamp(localOffset, -shapeExt, shapeExt);

                float3 toSource = localSource - localOffset;       // local space
                float3 toCenter = emitterPos  - wpos;              // world space
                float  d2source = dot(toSource, toSource);
                float  d2center = dot(toCenter, toCenter);

                // Falloff distance: closest-point on the shape, floored by a fraction of the box's
                // bounding-sphere squared. Prevents 1/0 blow-up both inside the box (d2source == 0) and
                // at sub-pixel distance from a box face (which would otherwise give a bright outline ring).
                // Collapses to max(d2source, 1e-4) for point emitters since shapeExt == 0.
                float distSqr = max(d2source, max(dot(shapeExt, shapeExt) * 0.25, 1e-4));

                // Range early-out: smooth factor is zero beyond range, skip rsqrt + dot + multiplies.
                half factor = half(distSqr * boxMin.w);            // distSqr / rangeSqr
                if (factor >= half(1.0)) continue;

                // Direction for the cosine term: always toward emitter center (volume-light approximation).
                // Closest-point direction collapses to face-perpendicular near the box and would zero out
                // the cosine for surfaces parallel to that face.
                half3 dir       = (half3)(toCenter * rsqrt(max(d2center, 1e-4)));
                half  normAtten = saturate(dot(dir, norm));

                half smoothFactor = saturate(half(1.0) - factor * factor);
                smoothFactor *= smoothFactor;
                half distAtten = half(rcp(distSqr)) * smoothFactor;

                sum = max(sum, (normAtten * distAtten) * _EmittersColors[k]);
            }
            return sum;
        }


    #endif

    #define TEST_DEPTH(lowestDiff, nearestColor, depthDiff, color) if (depthDiff < lowestDiff) { lowestDiff = depthDiff; nearestColor = color; }

    half4 GetIndirect(float2 uv, float depth) {        
        half4 nearestColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv);

        half depthM = nearestColor.w;
        half diff = abs(depth - depthM);

        UNITY_BRANCH
        if (diff > 0.00001) {
            float m = 0.5;

            float2 uvN = uv + float2(0, _MainTex_TexelSize.y * m );
            float2 uvS = uv - float2(0, _MainTex_TexelSize.y * m);
            float2 uvE = uv + float2(_MainTex_TexelSize.x * m, 0);
            float2 uvW = uv - float2(_MainTex_TexelSize.x * m, 0);

            half4 colorN = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvN, 0);
            half4 colorS = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvS, 0);
            half4 colorE = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvE, 0);
            half4 colorW = SAMPLE_TEXTURE2D_X_LOD(_MainTex, sampler_LinearClamp, uvW, 0);

            half4 depths = half4(colorN.w, colorS.w, colorE.w, colorW.w);
            half4 dDiff = abs(depths - depth.xxxx);

            half lowestDiff = diff;
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.x, colorN);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.y, colorS);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.z, colorE);
            TEST_DEPTH(lowestDiff, nearestColor, dDiff.w, colorW);

        }
        return nearestColor;
    }

	half4 FragUpscale (VaryingsRGI input): SV_Target {

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

        float rawDepth = GetRawDepth(uv);
        if (IsSkyBox(rawDepth)) return 0; // exclude skybox
        float depth = RawToLinearEyeDepth(rawDepth);

        float4 res = GetIndirect(uv, depth);
        return res;
	}


	half4 FragCompose (VaryingsRGI i) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(i);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
        float2 uv     = UnityStereoTransformScreenSpaceTex(i.uv);

        #if defined(DEBUG_GI)
            half4 input = half4(0, 0, 0, 0);
        #else
            half4 input = SAMPLE_TEXTURE2D_X_LOD(_InputRTGI, sampler_PointClamp, uv, 0);
        #endif
        
        float rawDepth = GetRawDepth(uv);
        if (IsSkyBox(rawDepth)) return input; // exclude skybox

        float depth = RawToLinearEyeDepth(rawDepth);

        // limit to volume bounds
        float3 wpos = GetWorldPosition(uv, rawDepth);
        if (IsOutsideBounds(wpos)) return input;

   	    half3 indirect = GetIndirect(uv, depth).rgb * INDIRECT_INTENSITY;
        
        half3 norm = GetWorldNormal(uv);

        // add virtual emitters
        #if _RGI_EMITTERS_ON
            indirect += GetVirtualEmitters(wpos, norm, (uint2)i.positionCS.xy);
        #endif

        // max brightness
        half lumaIndirect = GetLuma(indirect);
        indirect *= saturate(LUMA_MAX / (lumaIndirect + 0.001));

        float3 cameraPosition = GetCameraPositionWS();
        half3 toCamera = normalize(cameraPosition - wpos);
        half ndot = abs(dot(norm, toCamera));

        half surfaceOcclusion = 1;

        #if _FORWARD
            half4 pixel = max(0, SAMPLE_TEXTURE2D_X(_InputRTGI, sampler_LinearClamp, uv));
            half3 hue = normalize(pixel.rgb + 0.01);
            indirect = indirect * hue;
        #elif _FORWARD_AND_DEFERRED
            half occlusion;
            half3 brdfDiffuse;
            GetGBufferDiffuse(uv, brdfDiffuse, occlusion);
            if (all(brdfDiffuse == 0)) {
                half4 pixel = max(0, SAMPLE_TEXTURE2D_X_LOD(_InputRTGI, sampler_LinearClamp, uv, 0));
                half3 hue = normalize(pixel.rgb + 0.01);
                indirect = indirect * hue;
            } else {
                surfaceOcclusion = occlusion;
                indirect = indirect * brdfDiffuse;
            }
        #else
            half3 brdfDiffuse;
            GetGBufferDiffuse(uv, brdfDiffuse, surfaceOcclusion);
            indirect = indirect * brdfDiffuse;
        #endif

        half giAtten = lerp(1, surfaceOcclusion, OCCLUSION_INTENSITY);
        indirect *= giAtten;

        half aotermInfluence = 1;
        #if _SCREEN_SPACE_OCCLUSION
            half aoterm = GetScreenSpaceAmbientOcclusion(uv).indirectAmbientOcclusion;
            aotermInfluence = aoterm * AO_INFLUENCE + ONE_MINUS_AO_INFLUENCE;
            indirect *= aotermInfluence;
        #endif

        // reduce fog effect by enhancing normal mapping
        half normalAtten = lerp(1.0, ndot, NORMALS_INFLUENCE);
        indirect *= normalAtten;

        // optional GI weight
        half giLuma = GetLuma(indirect.rgb);
        input.rgb *= rcp(1.0 + giLuma * GI_WEIGHT);

        // saturate
        indirect = lerp(giLuma, indirect, COLOR_SATURATION);

        // attenuates near to camera
        half nearAtten = min(1.0, depth * NEAR_CAMERA_ATTENUATION);
        indirect *= nearAtten;

        // apply source brightness to base image
        input.rgb *= SOURCE_BRIGHTNESS;

        // legacy ambient subtraction (only when prepass is OFF and slider < 1)
        // The branch is uniform across all pixels, so the body is dynamically skipped
        // by the GPU when _LegacyAmbientSubtract == 0 (no shader variants added).
        UNITY_BRANCH
        if (_LegacyAmbientSubtract > 0.5) {
            half3 ambient = SampleAmbientLighting(wpos, norm, uv * SOURCE_SIZE);
            half3 ambientToSubtract;

            #if _FORWARD
                half4 pixelLA = max(0, SAMPLE_TEXTURE2D_X(_InputRTGI, sampler_LinearClamp, uv));
                half3 hueLA = normalize(pixelLA.rgb + 0.01);
                ambientToSubtract = ambient * hueLA;
            #elif _FORWARD_AND_DEFERRED
                half occlusionLA;
                half3 brdfDiffuseLA;
                GetGBufferDiffuse(uv, brdfDiffuseLA, occlusionLA);
                if (all(brdfDiffuseLA == 0)) {
                    half4 pixelLA = max(0, SAMPLE_TEXTURE2D_X_LOD(_InputRTGI, sampler_LinearClamp, uv, 0));
                    half3 hueLA = normalize(pixelLA.rgb + 0.01);
                    ambientToSubtract = ambient * hueLA;
                } else {
                    ambientToSubtract = ambient * brdfDiffuseLA;
                }
            #else
                half3 brdfDiffuseLA;
                half occlusionLA;
                GetGBufferDiffuse(uv, brdfDiffuseLA, occlusionLA);
                ambientToSubtract = ambient * brdfDiffuseLA;
            #endif

            ambientToSubtract *= giAtten;
            #if _SCREEN_SPACE_OCCLUSION
                ambientToSubtract *= aotermInfluence;
            #endif
            ambientToSubtract *= normalAtten;
            ambientToSubtract *= nearAtten;
            ambientToSubtract *= UNITY_AMBIENT_INTENSITY;

            input.rgb = max(input.rgb - ambientToSubtract, half3(0, 0, 0));
        }

        // add GI to input image
        input.rgb += indirect;

        #if _USES_NEAR_FIELD_OBSCURANCE
            half nfo = SAMPLE_TEXTURE2D_X(_NFO_RT, sampler_LinearClamp, uv).r;
            input.rgb = lerp(input.rgb, input.rgb * NEAR_FIELD_OBSCURANCE_TINT, saturate(nfo));
        #endif

        // dithering to reduce banding (optional)
        // half dither = frac(52.9829189 * frac(dot(i.positionCS.xy, half2(0.06711056, 0.00583715)))) / 255.0;
        // input.rgb += dither;

        return input;
	}


#endif // RGI_UPSCALE
