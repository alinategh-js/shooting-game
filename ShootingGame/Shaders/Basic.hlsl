cbuffer PerFrame : register(b0)
{
    row_major matrix World;
    row_major matrix WorldViewProjection;
    float4 Tint;
};

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float3 NormalW : TEXCOORD0;
    float  Depth : TEXCOORD1;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 worldPos = float4(input.Position, 1.0f);
    output.Position = mul(worldPos, WorldViewProjection);
    output.Color = input.Color * Tint;
    output.NormalW = normalize(mul(input.Normal, (float3x3)World));
    output.Depth = output.Position.z / max(output.Position.w, 1e-6);
    return output;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 L = normalize(float3(0.35, 0.85, 0.25));
    float ndl = saturate(dot(input.NormalW, L));
    float ambient = 0.22;
    float diffuse = 0.78 * ndl;

    // Subtle distance fog to make the room feel larger/alive.
    float fog = saturate((input.Depth - 0.25) * 0.6);
    float3 fogColor = float3(0.06, 0.09, 0.16);

    float3 lit = input.Color.rgb * (ambient + diffuse);
    lit = lerp(lit, fogColor, fog);
    return float4(lit, input.Color.a);
}
