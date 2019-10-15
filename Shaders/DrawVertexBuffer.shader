
Shader "Custom/DrawVertexBuffer"
{
	SubShader
	{
		/*Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }

		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha*/

		Pass
		{
			//Cull back

			CGPROGRAM
			#include "UnityCG.cginc"
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
			};


			// Vertex Shader
			v2f vert(uint id : SV_VertexID)
			{
				v2f OUT;
				
				Vert vert = vertexBuffer[id];

				OUT.pos = mul(mul(UNITY_MATRIX_VP, modelMatrix), vert.position);

				OUT.uv = vert.uv;
				
				return OUT;
			}

			sampler2D _MainTex;

			// Fragment Shader
			float4 frag(v2f IN) : SV_Target
			{
				return tex2D(_MainTex, IN.uv);
			}

			ENDCG

		}
	}
}