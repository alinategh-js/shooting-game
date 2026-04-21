using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ShootingGame.Editor;

/// <summary>
/// Immediate-mode 2D UI triangles in clip space (z=0, w=1). Draw after the 3D scene with depth disabled.
/// </summary>
public sealed class UiTriangleRenderer : IDisposable
{
    private static readonly uint Stride = (uint)Unsafe.SizeOf<UiVertex>();

    private readonly ID3D11Buffer _vertexBuffer;
    private readonly ID3D11VertexShader _vs;
    private readonly ID3D11PixelShader _ps;
    private readonly ID3D11InputLayout _il;
    private readonly ID3D11RasterizerState _rs;
    private readonly ID3D11DepthStencilState _depthOff;
    private readonly ID3D11BlendState _blendAlpha;

    public UiTriangleRenderer(ID3D11Device device)
    {
        // Dynamic VB large enough for a few hundred triangles (UI only).
        const int maxVerts = 4096;
        _vertexBuffer = device.CreateBuffer(
            (uint)(maxVerts * (int)Stride),
            BindFlags.VertexBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "UiSolid.hlsl");
        ReadOnlyMemory<byte> vsBc = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_5_0");
        ReadOnlyMemory<byte> psBc = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_5_0");
        _vs = device.CreateVertexShader(vsBc.Span);
        _ps = device.CreatePixelShader(psBc.Span);

        InputElementDescription[] elems =
        [
            new("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
        ];
        _il = device.CreateInputLayout(elems, vsBc.Span);

        _rs = device.CreateRasterizerState(
            new RasterizerDescription
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
            });

        _depthOff = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthFunc = ComparisonFunction.Always,
            });

        _blendAlpha = device.CreateBlendState(BlendDescription.AlphaBlend);
    }

    public unsafe void Draw(ID3D11DeviceContext context, ReadOnlySpan<UiVertex> vertices)
    {
        if (vertices.IsEmpty)
        {
            return;
        }

        MappedSubresource mapped = context.Map(_vertexBuffer, MapMode.WriteDiscard);
        var dst = new Span<UiVertex>(mapped.DataPointer.ToPointer(), vertices.Length);
        vertices.CopyTo(dst);
        context.Unmap(_vertexBuffer, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_il);
        context.IASetVertexBuffer(0, _vertexBuffer, Stride);
        context.VSSetShader(_vs);
        context.PSSetShader(_ps);
        context.RSSetState(_rs);
        context.OMSetDepthStencilState(_depthOff, 0);
        context.OMSetBlendState(_blendAlpha, new Color4(0, 0, 0, 0), uint.MaxValue);
        context.Draw((uint)vertices.Length, 0);
        context.OMSetBlendState(null, null, uint.MaxValue);
    }

    public void Dispose()
    {
        _blendAlpha.Dispose();
        _depthOff.Dispose();
        _rs.Dispose();
        _il.Dispose();
        _ps.Dispose();
        _vs.Dispose();
        _vertexBuffer.Dispose();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct UiVertex
{
    public Vector4 Position;
    public Color4 Color;

    public UiVertex(Vector4 position, Color4 color)
    {
        Position = position;
        Color = color;
    }
}
