// netmarble FUNNYPAW Ryu
// Unity 5.6.2 버전으로 수정.
// 캐릭터 전용 쉐이더 입니다.
Shader "SevenDeadlySins/Character Matcap" 
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

		[Space(5)]
		//[Header(Reflect)]
		[Toggle] _Reflect("Enalbled", Float) = 1
		_ReflColor("Reflect Color", Color) = (1.0,1.0,1.0,1.0)
		_ReflAmount("Reflect Amount", Range(0.0, 1.0)) = 0.5
		[Space(5)]
		_MaskTex("Mask Texture (RGBA)", 2D) = "black" { }
		[Space(5)]
		_MatcapReflR("MatCap Channel 'R'", 2D) = "black" { }
		[Toggle] _ReflectG("Enalbled", Float) = 0
		_MatcapReflG("MatCap Channel 'G'", 2D) = "black" { }
		[Toggle] _ReflectB("Enalbled", Float) = 0
		_MatcapReflB("MatCap Channel 'B'", 2D) = "black" { }
		[Toggle] _ReflectA("Enalbled", Float) = 0
		_MatcapReflA("MatCap Channel 'A'", 2D) = "black" { }

		[Header(Effect)]
		_DiffRot("Matcap Diffuse Rotation", Float) = 0
		_DamageColor("Damage Color", Color) = (1,1,1,0)			// 피격 될때 색상 변경 타이밍은 클라이언트에서 합니다.
		_OutlineColor("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth("Outline Width", Float) = 1
		_FXAddColor("FX Add Color", Color) = (0,0,0,1)			// 연출 Matcap 텍스쳐 색상을 더합니다.
		_FXMulColor("FX Multiply Color", Color) = (1,1,1,0)		// 연출 Matcap 텍스쳐 색상을 곱합니다.
		_FXMulAmount("FX Multiply Reflect Amount", Range(0.0, 1.0)) = 1.0 // 색상이 곱해질때 Reflect 값을 줄입니다.

	}

	Category
	{
		SubShader 
		{
			Tags
			{
				"Queue" = "Background"
				"LightMode" = "UniversalForward"
				"IgnoreProjector" = "False"
				"RenderType" = "Opaque"
			}
			Blend[_SrcBlend][_DstBlend]

			Pass	// pass drawing object
			{
				Cull Back

				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile __  _GLOBAL_CHARACTERLUT_ON
				#pragma multi_compile __  _CHARACTERLFX_ON
				#pragma multi_compile __ _REFLECT_ON
				#pragma multi_compile __ _REFLECTG_ON
				#pragma multi_compile __ _REFLECTB_ON
				#pragma multi_compile __ _REFLECTA_ON
				#pragma multi_compile __ _CHARACTERLUT_OFF

				#include "UnityCG.cginc"
				#include "Lighting.cginc"

				uniform half  _CharacterAlpha;
				uniform half4 _MainColor;
				uniform half4 _DamageColor;
				uniform half  _DiffRot;
				uniform float4x4 _RotationMatrix;

				sampler2D	  _MainTex;
				sampler2D	  _MatcapDiff;

				#ifdef	_REFLECT_ON
					uniform half4 _ReflColor;
					uniform half _ReflAmount;
					uniform half _FXMulAmount;
					sampler2D	  _MaskTex;
					sampler2D     _MatcapReflR;
					#ifdef	_REFLECTG_ON
						sampler2D     _MatcapReflG;
					#endif
					#ifdef	_REFLECTB_ON
						sampler2D     _MatcapReflB;
					#endif
					#ifdef	_REFLECTA_ON
						sampler2D     _MatcapReflA;
					#endif
				#endif
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
					// Set Texture Rotation
					o.uv.zw = lerp(o.uv.zw, (float2)mul(float3(o.uv.zw - 0.5f, 1.0f), (float3x3)_RotationMatrix), _DiffRot != 0);

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

					#ifdef	_REFLECT_ON
						fixed4 reflectMask = tex2D(_MaskTex, uv);
						fixed3 reflect = tex2D(_MatcapReflR, mcuv).rgb * reflectMask.rrr;
						fixed3 multiply = fixed3(1, 1, 1);

						#ifdef	_REFLECTG_ON
							fixed3 reflectG = tex2D(_MatcapReflG, mcuv).rgb;
							reflect += reflectG * reflectMask.ggg;
						#endif
						#ifdef	_REFLECTB_ON
							fixed3 reflectB = tex2D(_MatcapReflB, mcuv).rgb;
							multiply *= (reflectB * reflectMask.bbb) + (1.0f - reflectMask.bbb);
						#endif
						#ifdef	_REFLECTA_ON
							fixed3 reflectA = tex2D(_MatcapReflA, mcuv).rgb;
							multiply *= (reflectA * reflectMask.aaa) + (1.0f - reflectMask.aaa);
						#endif
					#endif

					////////// COLOR //////////
					fixed4 color;

					#ifdef	_REFLECT_ON
						color.rgb = (diffuse * difftex * _MainColor.rgb * multiply) * (_LightColor0 + 0.8f) + (reflect * _ReflColor.rgb * (_ReflAmount * _FXMulAmount));
					#else
						color.rgb = (diffuse * difftex * _MainColor.rgb) * (_LightColor0 + 0.8f);
					#endif

					color.a = _CharacterAlpha;

					#if defined	(_GLOBAL_CHARACTERLUT_ON) && (!_CHARACTERLUT_OFF)
						color.rgb = lerp(color.rgb, sampleAs3DTexture(_Global_characterLUT, saturate(color.rgb), _Global_characterLUT_Param.xyz), _Global_characterLUT_Param.w);
					#endif

					color.rgb = lerp(color.rgb, _DamageColor.rgb, _DamageColor.a);

					return color;
				}
				ENDCG
			}

			Pass	// Pass drawing outline
			{
				Cull Front

				CGPROGRAM
				#pragma target 3.0
				#pragma vertex vert
				#pragma fragment frag

				#include "UnityCG.cginc" 

				uniform half4 _OutlineColor;
				uniform half  _OutlineWidth;
				uniform half  _CharacterAlpha;

				struct v2f
				{
					half4 pos	 : POSITION;
				};
           
				v2f vert(appdata_full v)
				{
					v2f o;

					float t = unity_CameraProjection._m11;
					const float Rad2Deg = 180 / UNITY_PI;
					float fov = atan(1.0f / t) * 2.0 * Rad2Deg;

					float3 worldPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1.0f));
					fixed vertexGrayColor = v.color.r;
					half cameraDistance = distance(_WorldSpaceCameraPos, worldPos);

					worldPos += normalize(mul((float3x3)UNITY_MATRIX_M, v.normal)) * 0.002f * vertexGrayColor * pow(max(0.004f, cameraDistance - 0.15f), 0.5f) * pow(1.0f - ((60.0f - fov) / 60.0f), 0.5f) * max(_OutlineWidth, 0.004f);
					o.pos = mul(UNITY_MATRIX_VP, float4(worldPos.xyz, 1.0f));

					return o;
				}
           
				half4 frag(v2f i) :COLOR
				{
					return float4(_OutlineColor.rgb, _CharacterAlpha);
				}
				ENDCG
			}
        }
	}
	FallBack "Diffuse"
	//CustomEditor "CustomShaderMatcap"
}