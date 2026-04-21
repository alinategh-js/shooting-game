// Screen-space triangle: POSITION is clip-space xyzw (pre-divided semantics via w=1).
struct VSInput
{
    float4 Position : POSITION;
    float4 Color : COLOR;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

PSInput VSMain(VSInput input)
{
    PSInput o;
    o.Position = input.Position;
    o.Color = input.Color;
    return o;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    return input.Color;
}
