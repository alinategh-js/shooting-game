cbuffer PerFrame : register(b0)
{
    row_major matrix WorldViewProjection;
    float4 Tint;
};

struct VSInput
{
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 worldPos = float4(input.Position, 1.0f);
    output.Position = mul(worldPos, WorldViewProjection);
    output.Color = input.Color * Tint;
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
