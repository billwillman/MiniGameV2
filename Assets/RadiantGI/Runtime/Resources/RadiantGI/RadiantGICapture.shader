Shader "Hidden/Kronnect/RadiantGICapture" {
Properties {
}

HLSLINCLUDE
    #pragma target 3.0
ENDHLSL


Subshader {	

    ZWrite Off ZTest Always Cull Off
    Tags { "RenderPipeline" = "UniversalPipeline" }

    HLSLINCLUDE
    #pragma target 3.0
    #pragma prefer_hlslcc gles
    #pragma exclude_renderers d3d11_9x
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    ENDHLSL

  Pass {
      Name "World Positions"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragWorldPos
      #include "RadiantGICapture_WorldPos.hlsl"
      ENDHLSL
  }

  Pass {
      Name "Normals"
      HLSLPROGRAM
      #pragma vertex VertRGI
      #pragma fragment FragNormals
      #include "RadiantGICapture_Normals.hlsl"
      ENDHLSL
  }

}
FallBack Off
}
