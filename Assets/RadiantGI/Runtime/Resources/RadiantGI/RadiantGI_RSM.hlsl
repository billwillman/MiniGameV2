#ifndef RGI_WIDE
#define RGI_WIDE

// Copyright 2022-2026 Kronnect - All Rights Reserved.

TEXTURE2D_X(_RadiantShadowMapColors);
TEXTURE2D_X(_RadiantShadowMapNormals);
TEXTURE2D_X(_RadiantShadowMapWorldPos);
float4 _RadiantShadowMapColors_TexelSize;


TEXTURE2D_X(_RadiantShadowMapRSM);
float4x4 _RadiantWorldToShadowMap;

#define SAMPLE_COUNT 32
static float2 offsets[] = {
    float2(0.1092819, -1.2452140), float2(1.0755790, 1.4029010), float2(-2.1319680, -0.3771141),
    float2(2.1093880, -1.3418210), float2(-0.7256145, 2.6992570), float2(-1.4112360, -2.7172440), float2(3.1065140, 1.1344910),
    float2(-3.2680540, 1.3490090), float2(1.5894190, -3.3965060), float2(1.1830280, 3.7716650), float2(-3.5869800, -2.0787220),
    float2(4.2291300, -0.9297680), float2(-2.5920690, 3.6869620), float2(-0.6010606, -4.6382910), float2(3.7018480, 3.1199110),
    float2(-4.9957320, 0.2065974), float2(3.6532180, -3.6354530), float2(-0.2449574, 5.2976420), float2(-3.4909930, -4.1833590),
    float2(5.5402510, 0.7454209), float2(-4.7020520, 3.2715800), float2(1.2868110, -5.7200650), float2(2.9805090, 5.2013560),
    float2(-5.8340360, -1.8612000), float2(5.6736900, -2.6214070), float2(-2.4606510, 5.8796460), float2(-2.1983310, -6.1118640),
    float2(5.8549900, 3.0771960), float2(-6.5091140, 1.7158030), float2(3.7028470, -5.7588180), float2(1.1788310, 6.8591480),
    float2(-5.5905680, -4.3296190), float2(7.1561880, -0.5929008), float2(-4.9495820, 5.3503920), float2(0.0360481, -7.3950160),
    float2(5.0391630, 5.5548990), float2(-7.5710140, -0.7016501), float2(6.1378870, -4.6584760), float2(-1.3971590, 7.6802030),
    float2(-4.2106480, -6.6910780), float2(7.7192780, 2.1154980), float2(-7.2072710, 3.6986930), float2(2.8493120, -7.6856360),
    float2(3.1263100, 7.6796020), float2(-7.5774060, -3.5910310), float2(8.1015960, -2.4978510), float2(-4.3329320, 7.3934640),
    float2(-1.8182850, -8.4672280), float2(7.1334430, 5.0672090), float2(-8.7709830, 1.0931570), float2(5.7860390, -6.7977490),
    float2(0.3285438, 9.0078960), float2(-6.3875520, -6.4816530), float2(9.1736150, 0.4690015), float2(-7.1464080, 5.9047870),
    float2(1.2925040, -9.2644270), float2(5.3521360, 7.7728580), float2(-9.2773140, -2.1346290), float2(8.3538210, -4.7330090),
    float2(-2.9877480, 9.2099720), float2(-4.0515210, -8.8824490), float2(9.0608430, 3.8440030), float2(-9.3522910, 3.3124590)
};


float2 GetRSMCoords(float3 worldPos) {
    float4 shadowClipPos = mul(_RadiantWorldToShadowMap, float4(worldPos, 1.0));
    float2 uv = shadowClipPos.xy / shadowClipPos.w;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1.0 - uv.y;
    #endif
    return uv;
}

half4 FragRSM (VaryingsRGI input) : SV_Target  {
    
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    input.uv = UnityStereoTransformScreenSpaceTex(input.uv);
    float2 uv = input.uv;

    float rawDepth = GetRawDepth(uv);
    if (IsSkyBox(rawDepth)) return 0; // exclude skybox
   
    float3 worldPosM = GetWorldPosition(uv, rawDepth);
    half3 normalM = GetWorldNormal(uv);
    float2 rsmCoords = GetRSMCoords(worldPosM);

    float2 pos = uv * SOURCE_SIZE;
    float screenNoise = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_PointRepeat, pos * _NoiseTex_TexelSize.xy, 0).x;
    screenNoise = frac(screenNoise + GOLDEN_RATIO_ACUM);

    float si, co;
    sincos(screenNoise * 2.0 * PI, si, co);
    float2x2 rotMatrix = float2x2(co, -si, si, co);
    
    float texelMultiplier = _RadiantShadowMapColors_TexelSize.z / 128;
    float2 texelSize = _RadiantShadowMapColors_TexelSize.xy * texelMultiplier;
    half3 indirect = 0;
    half sumWeight = 0.0001;

    for (int k = 0; k < SAMPLE_COUNT; k++) {

        float2 offset = offsets[k];
        offset = mul(offset, rotMatrix);
        float2 uvN = rsmCoords + offset * texelSize;

        float2 noise = SAMPLE_TEXTURE2D_LOD(_NoiseTex, sampler_PointRepeat, uvN * SOURCE_SIZE, 0).xw;
        uvN += (noise.xy - 0.5) * 0.03;

        half4 colorN = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapColors, sampler_LinearClamp, uvN, 0);
        half3 normalN = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapNormals, sampler_PointClamp, uvN, 0).xyz;
        float3 worldPosN = SAMPLE_TEXTURE2D_X_LOD(_RadiantShadowMapWorldPos, sampler_LinearClamp, uvN, 0).xyz;

        half3 toM = worldPosM - worldPosN;
        half3 toMnorm = normalize(toM); // neighbour to center pixel
        half w = max(0, dot(toMnorm, normalN));

        half3 toNnorm = -toMnorm; // center pixel to neighbour
        w *= max(0, dot(toNnorm, normalM));

        // quadratic distance attenuation
        half dist = dot2(toM);
        half distSqr = 1.0 + dist;
        half distAtten = rcp(distSqr);
        half invDistSqrWeight = lerp(1.0, distAtten, INDIRECT_DISTANCE_ATTENUATION);
        w *= invDistSqrWeight;
        
        indirect += colorN.rgb * w;
        sumWeight += w;
    }

    indirect *= RSM_INTENSITY * rcp(sumWeight);
    return half4(indirect, 0.0);
   
}

#endif // RGI_RSM