// netmarble FUNNYPAW Ryu
// Unity 5.6.2 버전으로 수정.
// 캐릭터 전용 쉐이더 입니다.
Shader "SevenDeadlySins/Shadow" 
{
	Properties 
	{
		[Toggle] _Shadow("Shadow Enalbled", Float) = 1
		_ShadowColor("Shadow Color", Color) = (0.0,0.0,0.0,0.51)
		_ShadowLength("Shadow Length", Range(0.0, 0.9)) = 0
	    _ShadowHeight("Shadow Height", Float) = 0.01
		[HideInInspector]_CharacterAlpha("Character Alpha", Range(0.0, 1.0)) = 1.0
	}

	Category
	{
		SubShader 
		{
			Tags
			{
				"Queue" = "Transparent-300"
				"IgnoreProjector" = "True"
				"LightMode" = "UniversalForward"
				"RenderType" = "Transparent"
			}

			ZWrite On
			ZTest LEqual
			Blend SrcAlpha OneMinusSrcAlpha

			// 매쉬가 겹치는 부분을 그리지 않기위함
			Stencil
			{
				Ref 0
				Comp Equal
				Pass IncrWrap
				ZFail IncrWrap
			}

			Pass
			{
				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __ _SHADOW_ON

				#include "UnityCG.cginc"

				uniform half  _CharacterAlpha;
				uniform half4 _ShadowColor;
				uniform half  _ShadowLength;
				uniform float  _ShadowHeight;

				struct v2f
				{
					float4 pos	: SV_POSITION;
				};

				v2f vert(appdata_base v)
				{
					v2f o;

					#ifdef _SHADOW_ON
						float4 vPosWorld = mul(unity_ObjectToWorld, v.vertex);
						half4 lightDirection = -normalize(_WorldSpaceLightPos0);
						float opposite = vPosWorld.y - _ShadowHeight;
						half cosTheta = 1.0f - _ShadowLength;	// -lightDirection.y
						float hypotenuse = opposite / cosTheta;
						float3 vPos = vPosWorld.xyz + (lightDirection * hypotenuse);

						o.pos = mul(UNITY_MATRIX_VP, float4(vPos.x, _ShadowHeight, vPos.z, 1));
					#else
						o.pos = float4(100, 0, 0, 1);
					#endif

					return o;
				}

				half4 frag(v2f i) : COLOR
				{
					return _ShadowColor;
				}
				ENDCG
			}
        }
	}
	FallBack "Diffuse"
}