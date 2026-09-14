Shader "Hidden/Kronnect/RadiantGIOrganicLight"
{
SubShader
{
    ZWrite Off ZTest Always Blend Off Cull Off

    HLSLINCLUDE
    #pragma target 3.0
    #pragma prefer_hlslcc gles
    #pragma exclude_renderers d3d11_9x
    
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#if UNITY_VERSION >= 60010000
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #define UnpackMaterialFlags UnpackGBufferMaterialFlags
#else
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#endif
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

    #include "RadiantGI_Common.hlsl"
    ENDHLSL

  Pass { // 0
      Name "Radiant GI Organic Light Pass"
      Blend One One
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragOrganicLight
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #pragma multi_compile_local _ _DISTANCE_BLENDING
      #include "RadiantGIOrganicLightPass.hlsl"
      ENDHLSL
  }

}
}


	