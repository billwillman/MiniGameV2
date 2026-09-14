Shader "Hidden/FogOfWarHardwareCombine"
{
	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off
		Fog { Mode Off }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			sampler2D _VisibleTex;
			float4 _VisibleTex_TexelSize;
			sampler2D _PartialTex;
			float4 _PartialTex_TexelSize;
			sampler2D _VisibleOnlyTex;
			float4 _VisibleOnlyTex_TexelSize;

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 position : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			v2f vert(appdata v)
			{
				v2f o;
				o.position = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				return fixed4(tex2D(_VisibleTex, i.uv).r, tex2D(_PartialTex, i.uv).r, tex2D(_VisibleOnlyTex, i.uv).r, 1);
			}
			ENDCG
		}
	}
}
