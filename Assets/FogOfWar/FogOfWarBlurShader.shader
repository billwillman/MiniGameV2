// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Hidden/FogOfWarBlurShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BlurAmount ("Blur Amount", float) = 0.1
	}
	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off
		Fog { Mode Off }

		Pass
		{
			CGPROGRAM
			#pragma multi_compile GAUSSIAN3 GAUSSIAN5 ANTIALIAS

			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_TexelSize;
			float _BlurAmount;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed3 frag (v2f i) : SV_Target
			{
				float2 pixelsize = _MainTex_TexelSize.xy;
				#define GETPIXEL(xx, yy) tex2D(_MainTex, i.uv + float2(pixelsize.x * xx, pixelsize.y * yy)).rgb
				#define CONVOLUTION(xx, yy, m) GETPIXEL(xx, yy) * m

				#if GAUSSIAN3 // Gaussian blur 3x3
					#define CONVOLUTION1(y, a, b, c) CONVOLUTION(-1, y, a) + CONVOLUTION(0, y, b) + CONVOLUTION(1, y, c)

					float3 color = CONVOLUTION1(-1, 1, 2, 1);
					color += CONVOLUTION1(0, 2, 4, 2);
					color += CONVOLUTION1(1, 1, 2, 1);
					color *= 1.0 / 16;

				#elif GAUSSIAN5 // Gaussian blur 5x5
					#define CONVOLUTION2(y, a, b, c, d, e) CONVOLUTION(-2, y, a) + CONVOLUTION(-1, y, b) + CONVOLUTION(0, y, c) + CONVOLUTION(1, y, d) + CONVOLUTION(2, y, e)

					float3 color = CONVOLUTION2(-2, 1, 4, 6, 4, 1);
					color += CONVOLUTION2(-1, 4, 16, 24, 16, 4);
					color += CONVOLUTION2(0, 6, 24, 36, 24, 6);
					color += CONVOLUTION2(1, 4, 16, 24, 16, 4);
					color += CONVOLUTION2(2, 1, 4, 6, 4, 1);
					color *= 1.0 / 256;

				#else//if ANTIALIAS // Antialiasing
					#define CONVOLUTION1(y, a, b, c) CONVOLUTION(-1, y, a) + CONVOLUTION(0, y, b) + CONVOLUTION(1, y, c)

					// find edge
					float3 fog11 = GETPIXEL(-1, -1);
					float3 fog21 = GETPIXEL(0, -1);
					float3 fog31 = GETPIXEL(1, -1);
					float3 fog12 = GETPIXEL(-1, 0);
					float3 fog22 = GETPIXEL(0, 0);
					float3 fog32 = GETPIXEL(1, 0);
					float3 fog13 = GETPIXEL(-1, 1);
					float3 fog23 = GETPIXEL(0, 1);
					float3 fog33 = GETPIXEL(1, 1);

					float3 edge = fog11 + fog12 + fog13 + fog21 + fog23 + fog31 + fog32 + fog33;
					edge = -edge + fog22 * 8;

					// find blur
					float3 blur = (fog11 + fog13 + fog31 + fog33) + (fog12 + fog21 + fog32 + fog23) * 2 + fog22 * 4;
					blur *= 1.0f / 16;

					// antialias
					//float color = saturate(edge);
					float3 color = lerp(fog22, blur, saturate(edge));
				#endif

				return color;
			}
			ENDCG
		}
	}
}
