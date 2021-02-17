
Shader "Custom/DrawVertexBuffer"
{
	Properties
	{
		[Header(Chroma Key)]
		[Toggle][KeyEnum(Off, On)] _EnableChromaKey("Enable ChromaKey", Float) = 1
		_ChromaKeyColor("Color", Color) = (0.0, 1.0, 0.0, 1.0)
		_ChromaKeyHueRange("Hue Range", Range(0, 1)) = 0.1
		_ChromaKeySaturationRange("Saturation Range", Range(0, 1)) = 0.5
		_ChromaKeyBrightnessRange("Brightness Range", Range(0, 1)) = 0.5
	}

	SubShader
	{
		Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }

		//ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			//Cull back

			CGPROGRAM
			#include "UnityCG.cginc"
			#include "./ChromaKey.cginc"
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag

			struct Vert
			{
				float4 position;
				float2 uv;
			};

			uniform StructuredBuffer<Vert> vertexBuffer;

			float4x4 modelMatrix;

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORDS0;
				float alpha : TEXCCORDS1;
			};


			// Vertex Shader
			v2f vert(uint id : SV_VertexID)
			{
				v2f OUT;
				
				Vert vert = vertexBuffer[id];

				OUT.alpha = vert.position.a;

				vert.position.a = 1;

				OUT.pos = mul(mul(UNITY_MATRIX_VP, modelMatrix), vert.position);

				OUT.uv = vert.uv;

				
				
				return OUT;
			}

			sampler2D _MainTex;
			float _EnableChromaKey;

			// Fragment Shader
			float4 frag(v2f IN) : SV_Target
			{
				float4 color;

				color = tex2D(_MainTex, IN.uv);


				if (_EnableChromaKey)
				{
					ChromaKeyApplyAlpha(color);
				}

				
				bool debug = false;

				if (!debug)
				{
					color.a = IN.alpha;
				}
				else
				{
					if (IN.alpha < 1)
					{
						color = float4(0.5 + 0.5*(1 - IN.alpha), 0, 0, 1);
					}
					else
					{
						color = float4(0, 1, 0, 1);
					}
				}
				
				return color;
			}

			ENDCG

		}
	}
}