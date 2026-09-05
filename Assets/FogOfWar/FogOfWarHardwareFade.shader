Shader "Hidden/FogOfWarHardwareFade"
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
			sampler2D _LastVisibleTex;
			float4 _LastVisibleTex_TexelSize;
			float _FadeAmount;

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

			v2f vert (appdata v)
			{
				v2f o;
				o.position = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			float frag (v2f i) : SV_Target
			{
				float visible = tex2D(_VisibleTex, i.uv).r;
				float lastvisible = tex2D(_LastVisibleTex, i.uv).r;
	
				float diff = (visible - lastvisible) * _FadeAmount;
				return saturate(lastvisible + diff);
			}
			ENDCG
		}
	}
}
