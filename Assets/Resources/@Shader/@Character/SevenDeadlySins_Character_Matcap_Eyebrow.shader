// netmarble FUNNYPAW Ryu
// Unity 5.6.2 버전으로 수정.
// 캐릭터 전용 쉐이더 입니다.
Shader "SevenDeadlySins/Character Matcap Eyebrow" 
{
	Properties 
	{
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Blend Source", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Blend Destination", Float) = 0
		_CharacterAlpha("Character Alpha", Range(0.0, 1.0)) = 1.0

		//[Header(Diffuse)]
		_MainColor("Main Color", Color) = (1.0,1.0,1.0,1.0)
		[Space(5)]
		_MainTex("Base Texture (RGB)", 2D) = "" { }
		_MatcapDiff("Diffuse MatCap", 2D) = "white" { }

		[Header(Effect)]
		_DamageColor("Damage Color", Color) = (1,1,1,0)			// 피격 될때 색상 변경 타이밍은 클라이언트에서 합니다.
		_FXAddColor("FX Add Color", Color) = (0,0,0,1)			// 연출 Matcap 텍스쳐 색상을 더합니다.
		_FXMulColor("FX Multiply Color", Color) = (1,1,1,0)		// 연출 Matcap 텍스쳐 색상을 곱합니다.
	}

	Category
	{
		SubShader 
		{	
			Tags
			{
				"Queue" = "Background-100"
				"LightMode" = "UniversalForward"
				"IgnoreProjector" = "False"
				"RenderType" = "Opaque"
			}
			Cull Back

			Pass	// pass drawing face eyebrow
			{
				Blend[_SrcBlend][_DstBlend]
				ZTest LEqual

				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __  _GLOBAL_CHARACTERLUT_ON
				#pragma multi_compile __  _CHARACTERLFX_ON
				#pragma multi_compile __ _CHARACTERLUT_OFF

				#include "UnityCG.cginc"
				#include "Lighting.cginc"		

				uniform half  _CharacterAlpha;
				uniform half4 _MainColor;
				uniform half4 _DamageColor;

				sampler2D	  _MainTex;
				sampler2D	  _MatcapDiff;

				#if defined	(_GLOBAL_CHARACTERLUT_ON) && (!_CHARACTERLUT_OFF)
					uniform half4 _Global_characterLUT_Param;
					sampler2D     _Global_characterLUT;
				#endif
				#ifdef	_CHARACTERLFX_ON
					sampler2D     _CharacterFX;
					uniform half4 _FXAddColor;
					uniform half4 _FXMulColor;
				#endif

				struct v2f 
				{

					half4 pos      : POSITION;
					float4 uv 	   : TEXCOORD0;
				};

				// Vertex shader function.
				v2f vert(appdata_full v)
				{
					v2f o;

					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv.xy = v.texcoord.xy;

					float3 worldNorm = normalize(unity_WorldToObject[0].xyz * v.normal.x + unity_WorldToObject[1].xyz * v.normal.y + unity_WorldToObject[2].xyz * v.normal.z);
					worldNorm = mul((float3x3)UNITY_MATRIX_V, worldNorm);
					o.uv.zw = (worldNorm.xy * 0.5) * v.color.a + 0.5;
						
					return o;
				}

				half3 sampleAs3DTexture(sampler2D tex, half3 uvw, half3 scaleOffset)
				{
					// Strip format where `height = sqrt(width)`
					uvw.z *= scaleOffset.z;
					half shift = floor(uvw.z);
					uvw.xy = uvw.xy * scaleOffset.z * scaleOffset.xy + scaleOffset.xy * 0.5;
					uvw.x += shift * scaleOffset.y;
					uvw.xyz = lerp(tex2D(tex, uvw.xy).rgb, tex2D(tex, uvw.xy + half2(scaleOffset.y, 0)).rgb, uvw.z - shift);
					return uvw;
				}

				// Fragment shader function.
				fixed4 frag(v2f i) : SV_Target
				{
					const float2 uv = i.uv.xy;
					const float2 mcuv = i.uv.zw;

					fixed3 difftex = tex2D(_MainTex, uv).rgb;
					fixed3 diffuse = tex2D(_MatcapDiff, mcuv).rgb;

					#ifdef	_CHARACTERLFX_ON
						diffuse = lerp(diffuse, diffuse * _FXMulColor.rgb, _FXMulColor.a);

						fixed3 diffuseFX = tex2D(_CharacterFX, mcuv).rgb * _FXAddColor.rgb;
						diffuse += diffuseFX * _FXAddColor.a;
					#endif

					////////// COLOR //////////
					fixed4 color;

					color.rgb = (diffuse * difftex * _MainColor) * (_LightColor0 + 0.8f);
					color.a = _CharacterAlpha;

					#if defined	(_GLOBAL_CHARACTERLUT_ON) && (!_CHARACTERLUT_OFF)
						color.rgb = lerp(color.rgb, sampleAs3DTexture(_Global_characterLUT, saturate(color.rgb), _Global_characterLUT_Param.xyz), _Global_characterLUT_Param.w);
					#endif

					color.rgb = lerp(color.rgb, _DamageColor.rgb, _DamageColor.a);

					return color;
				}
				ENDCG
			}

			Pass	// pass drawing face eyebrow
			{
				Blend SrcAlpha OneMinusSrcAlpha
				ZTest GEqual
				ZWrite Off

				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __  _GLOBAL_CHARACTERLUT_ON
				#pragma multi_compile __  _CHARACTERLFX_ON
				#pragma multi_compile __ _CHARACTERLUT_OFF

				#include "UnityCG.cginc"
				#include "Lighting.cginc"		

				uniform half  _CharacterAlpha;
				uniform half4 _MainColor;
				uniform half4 _DamageColor;

				sampler2D	  _MainTex;
				sampler2D	  _MatcapDiff;

				#if defined	(_GLOBAL_CHARACTERLUT_ON) && (!_CHARACTERLUT_OFF)
					uniform half3 _Global_characterLUT_Param;
					sampler2D     _Global_characterLUT;
				#endif
				#ifdef	_CHARACTERLFX_ON
					sampler2D     _CharacterFX;
					uniform half4 _FXAddColor;
					uniform half4 _FXMulColor;
				#endif

				struct v2f 
				{

					half4 pos      : POSITION;
					float4 uv 	   : TEXCOORD0;
				};

				// Vertex shader function.
				v2f vert(appdata_full v)
				{
					v2f o;

					o.pos = UnityObjectToClipPos(v.vertex);
					o.uv.xy = v.texcoord.xy;

					float3 worldNorm = normalize(unity_WorldToObject[0].xyz * v.normal.x + unity_WorldToObject[1].xyz * v.normal.y + unity_WorldToObject[2].xyz * v.normal.z);
					worldNorm = mul((float3x3)UNITY_MATRIX_V, worldNorm);
					o.uv.zw = (worldNorm.xy * 0.5) * v.color.a + 0.5;
						
					return o;
				}

				half3 sampleAs3DTexture(sampler2D tex, half3 uvw, half3 scaleOffset)
				{
					// Strip format where `height = sqrt(width)`
					uvw.z *= scaleOffset.z;
					half shift = floor(uvw.z);
					uvw.xy = uvw.xy * scaleOffset.z * scaleOffset.xy + scaleOffset.xy * 0.5;
					uvw.x += shift * scaleOffset.y;
					uvw.xyz = lerp(tex2D(tex, uvw.xy).rgb, tex2D(tex, uvw.xy + half2(scaleOffset.y, 0)).rgb, uvw.z - shift);
					return uvw;
				}

				// Fragment shader function.
				fixed4 frag(v2f i) : SV_Target
				{
					const float2 uv = i.uv.xy;
					const float2 mcuv = i.uv.zw;

					fixed3 difftex = tex2D(_MainTex, uv).rgb;
					fixed3 diffuse = tex2D(_MatcapDiff, mcuv).rgb;

					#ifdef	_CHARACTERLFX_ON
						diffuse = lerp(diffuse, diffuse * _FXMulColor.rgb, _FXMulColor.a);

						fixed3 diffuseFX = tex2D(_CharacterFX, mcuv).rgb * _FXAddColor.rgb;
						diffuse += diffuseFX * _FXAddColor.a;
					#endif

					////////// COLOR //////////
					fixed4 color;

					color.rgb = (diffuse * difftex * _MainColor) * (_LightColor0 + 0.8f);
					color.a = 0.6f * _CharacterAlpha;

					#if defined	(_GLOBAL_CHARACTERLUT_ON) && (!_CHARACTERLUT_OFF)
						color.rgb = sampleAs3DTexture(_Global_characterLUT, saturate(color.rgb), _Global_characterLUT_Param);
					#endif

					color.rgb = lerp(color.rgb, _DamageColor.rgb, _DamageColor.a);

					return color;
				}
				ENDCG
			}
        }
	}
	FallBack "Diffuse"
	//CustomEditor "CustomShaderEyebrow"
}