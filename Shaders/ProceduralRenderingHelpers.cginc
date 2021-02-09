#include "UnityCG.cginc"

struct Vert
{
	float4 position;
	float2 uv;
};

float2 DecodeUV(float encoded_uv)
{
	uint temp = (uint)encoded_uv;
	uint u = (temp & 0xFFFF0000) >> 16;
	uint v = temp & 0x0000FFFF;

	return float2((float)u / 65535.0, (float)v / 65535.0);
}

Vert CreateVertex(float4 v, float confidence)
{
	Vert vert;

	vert.position.x = v.x;
	vert.position.y = v.y;
	vert.position.z = v.z;
	//vert.position.w = 1.0f;
	vert.position.w = confidence;

	vert.uv = DecodeUV(v.w);

	return vert;
}

bool ShouldPointBeDiscarded(matrix<float, 4, 4> points_to_clipping_box, float3 p)
{
	float4 p_ = mul(points_to_clipping_box, float4(p.xyz, 1.0));

	return p_.x < -1.0 || p_.x > 1.0 || p_.y < -1.0 || p_.y > 1.0 || p_.z < -1.0 || p_.z > 1.0;
}

float EncodeUV(float2 uv)
{
	//quantize from 0-1 floats, to 0-65535 integers, which can be represented in 16 bits
	uint u = (uint)round(uv.x * 65535.0);
	uint v = (uint)round(uv.y * 65535.0);

	uint encoded = u << 16 | v;

	return (float)encoded;
}