Shader "Hidden/Kronnect/RadiantGI_URP" {

Properties {
    [NoScaleoffset] _NoiseTex("Noise Tex", any) = "" {}
    _StencilValue("Stencil Value", Int) = 0
    _StencilCompareFunction("Stencil Compare Function", Int) = 8
}

Subshader {	

    ZWrite Off ZTest Always Cull Off
    Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

    HLSLINCLUDE
    #pragma target 3.0
    #pragma prefer_hlslcc gles
    #pragma exclude_renderers d3d11_9x
    //#pragma enable_d3d11_debug_symbols

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
#if UNITY_VERSION >= 60010000
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #define UnpackMaterialFlags UnpackGBufferMaterialFlags
#else
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#endif

    #undef SAMPLE_TEXTURE2D_X
    #define SAMPLE_TEXTURE2D_X(tex,sampler,uv) SAMPLE_TEXTURE2D_X_LOD(tex,sampler,uv,0)

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    #include "RadiantGI_Common.hlsl"

    ENDHLSL

  Pass { // 0
      Name "Copy Exact"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCopyExact
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 1
      Name "Raycast"
      HLSLPROGRAM
      #pragma vertex VertRaycast
      #pragma fragment FragRaycast
      #pragma multi_compile_local _ _USES_MULTIPLE_RAYS
      #if UNITY_VERSION >= 60010000
        #pragma multi_compile_local _ _FALLBACK_1_PROBE _FALLBACK_2_PROBES _FALLBACK_PROBE_ATLAS
        #pragma multi_compile_fragment _ _CLUSTER_LIGHT_LOOP
      #else
        #pragma multi_compile_local _ _FALLBACK_1_PROBE _FALLBACK_2_PROBES
      #endif
      #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
      #pragma multi_compile_local _ _REUSE_RAYS
      #pragma multi_compile_local _ _ONE_EXTRA_BOUNCE
      #pragma multi_compile_local _ _FALLBACK_RSM
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #if UNITY_VERSION >= 202310
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
      #endif
      #include "RadiantGI_Raycast.hlsl"
      ENDHLSL
  }

  Pass { // 2
      Name "Light Buffer Accumulation"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragRGI
      #include "RadiantGI_TAcum.hlsl"
      ENDHLSL
  }

  Pass { // 3
      Name "Albedo"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragAlbedo
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 4
      Name "Normals"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragNormals
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 5
      Name "Compose"
      Stencil {
        Ref [_StencilValue]
        Comp [_StencilCompareFunction]
      }
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCompose
      #pragma multi_compile_local _ _FORWARD _FORWARD_AND_DEFERRED
      #pragma multi_compile_local _ _VIRTUAL_EMITTERS _VIRTUAL_EMITTERS_LAYERS
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #pragma multi_compile_local _ _USES_NEAR_FIELD_OBSCURANCE
      #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #if UNITY_VERSION >= 202310
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
      #endif
      #include "RadiantGI_Upscale.hlsl"
      ENDHLSL
  }

  Pass { // 6
      Name "Compare Mode"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCompare
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 7
      Name "Final GI Debug"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCompose
      #pragma multi_compile_local _ _FORWARD _FORWARD_AND_DEFERRED
      #pragma multi_compile_local _ _VIRTUAL_EMITTERS _VIRTUAL_EMITTERS_LAYERS
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #pragma multi_compile_local _ _USES_NEAR_FIELD_OBSCURANCE
      #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #if UNITY_VERSION >= 202310
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
      #endif
      #define DEBUG_GI
      #include "RadiantGI_Upscale.hlsl"
      ENDHLSL
  }

  Pass { // 8
      Name "Copy"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCopy
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 9
      Name "Wide filter"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragWideBlur
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #pragma multi_compile_local_fragment _ _BLUR_QUALITY_HIGHEST
      #define RGI_BLUR_HORIZ
      #include "RadiantGI_WideKernelFilter.hlsl"
      ENDHLSL
  }

  Pass { // 10
      Name "Depth"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragDepth
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 11
      Name "Copy Depth"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragCopyDepth
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #pragma multi_compile_local _ _TRANSPARENT_DEPTH_PREPASS
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 12
      Name "Reflective Shadow Map"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragRSM
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #include "RadiantGI_RSM.hlsl"
      ENDHLSL
  }

  Pass { // 13
      Name "Near Field Obscurance"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragRGI
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #include "RadiantGI_NFO.hlsl"
      ENDHLSL
  }

  Pass { // 14
      Name "Near Field Obscurance Blur"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragNFOWideBlur
      #pragma multi_compile_local _ _ORTHO_SUPPORT
      #include "RadiantGI_WideKernelFilter.hlsl"
      ENDHLSL
  }

  Pass { // 15
      Name "Ray Buffer Accumulation"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragRGI2
      #include "RadiantGI_TAcum.hlsl"
      ENDHLSL
  }

  Pass { // 16
      Name "Motion Vectors"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragMotion
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }

  Pass { // 17
      Name "Ambient Strip"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragAmbientStrip
      #pragma multi_compile_local _ _FORWARD _FORWARD_AND_DEFERRED
      #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
      #if UNITY_VERSION >= 202310
      #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
      #endif
      #include "RadiantGI_AmbientStrip.hlsl"
      ENDHLSL
  }

  Pass { // 18
      Name "Rendering Layer Mask"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragRenderingLayerMask
      #include "RadiantGI_Blends.hlsl"
      ENDHLSL
  }
}
}
