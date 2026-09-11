Shader "ClusterMesh/SkinnedLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Scale", Float) = 1
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .5
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass { Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ClusterSkinnedVert
            #pragma fragment ClusterSkinnedFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterSkinnedSetup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #include "ClusterSkinnedMeshLit.hlsl"
            ENDHLSL }
        Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ClusterSkinnedShadowVert
            #pragma fragment ClusterSkinnedShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterSkinnedSetup
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "ClusterSkinnedMeshLit.hlsl"
            ENDHLSL }
        Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" } ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ClusterSkinnedDepthVert
            #pragma fragment ClusterSkinnedShadowFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterSkinnedSetup
            #include "ClusterSkinnedMeshLit.hlsl"
            ENDHLSL }
        // ClusterMesh deferred support is intentionally URP-only.
        Pass { Name "GBuffer" Tags { "LightMode"="UniversalGBuffer" "UniversalMaterialType"="Lit" }
            ZWrite On ZTest LEqual
            Stencil { Ref 32 ReadMask 96 WriteMask 96 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex ClusterSkinnedVert
            #pragma fragment ClusterSkinnedGBufferFrag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterSkinnedSetup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ UNITY_TUANJIEGI
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #define CLUSTERMESH_GBUFFER_PASS 1
            #include "ClusterSkinnedMeshLit.hlsl"
            ENDHLSL }
    }
    FallBack Off
}
