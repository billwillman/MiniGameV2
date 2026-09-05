Shader "ClusterMesh/Lit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Scale", Float) = 1
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0
        [HideInInspector] _EnableClusterColor("Enable Cluster Color", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex ClusterMeshVert
            #pragma fragment ClusterMeshFrag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterMeshSetup
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "ClusterMeshLit.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ClusterMeshShadowVert
            #pragma fragment ClusterMeshShadowFrag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterMeshSetup
            #include "ClusterMeshLit.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ClusterMeshShadowVert
            #pragma fragment ClusterMeshShadowFrag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ClusterMeshSetup
            #include "ClusterMeshLit.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
