cbuffer PerDraw : register(b0)
{
    row_major matrix WorldViewProjection;
    float4 Color;
};

struct VSInput
{
    float3 Position : POSITION;
};

struct PSInput
{
    float4 Position : SV_POSITION;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 p = float4(input.Position, 1.0f);
    output.Position = mul(p, WorldViewProjection);
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return Color;
}
