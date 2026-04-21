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
/// Draws axis-aligned box edges in world space (line list). Separate from voxel cubes for clarity/refactors.
/// </summary>
public sealed class LinePrimitiveRenderer : IDisposable
{
    private static readonly uint Stride = (uint)Unsafe.SizeOf<Vector3>();

    private readonly ID3D11Buffer _vertexBuffer;
    private readonly ID3D11Buffer _indexBuffer;
    private readonly ID3D11Buffer _constantBuffer;
    private readonly ID3D11VertexShader _vs;
    private readonly ID3D11PixelShader _ps;
    private readonly ID3D11InputLayout _il;
    private readonly ID3D11RasterizerState _rs;
    private readonly ID3D11DepthStencilState _depth;

    public LinePrimitiveRenderer(ID3D11Device device)
    {
        float e = 1f; // unit box - scale with world matrix per draw if needed; we bake extent in verts
        var min = new Vector3(-e, -e, -e);
        var max = new Vector3(e, e, e);

        Vector3 c000 = new(min.X, min.Y, min.Z);
        Vector3 c100 = new(max.X, min.Y, min.Z);
        Vector3 c110 = new(max.X, max.Y, min.Z);
        Vector3 c010 = new(min.X, max.Y, min.Z);
        Vector3 c001 = new(min.X, min.Y, max.Z);
        Vector3 c101 = new(max.X, min.Y, max.Z);
        Vector3 c111 = new(max.X, max.Y, max.Z);
        Vector3 c011 = new(min.X, max.Y, max.Z);

        ReadOnlySpan<Vector3> verts =
        [
            c000, c100, c110, c010, c001, c101, c111, c011,
        ];

        ReadOnlySpan<uint> idx =
        [
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7,
        ];

        _vertexBuffer = device.CreateBuffer(verts, BindFlags.VertexBuffer);
        _indexBuffer = device.CreateBuffer(idx, BindFlags.IndexBuffer);

        uint cbSize = (uint)Unsafe.SizeOf<LineConstants>();
        _constantBuffer = device.CreateBuffer(cbSize, BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Line.hlsl");
        ReadOnlyMemory<byte> vsBc = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_5_0");
        ReadOnlyMemory<byte> psBc = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_5_0");
        _vs = device.CreateVertexShader(vsBc.Span);
        _ps = device.CreatePixelShader(psBc.Span);

        InputElementDescription[] elems =
        [
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0),
        ];
        _il = device.CreateInputLayout(elems, vsBc.Span);

        _rs = device.CreateRasterizerState(
            new RasterizerDescription
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
            });

        _depth = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthFunc = ComparisonFunction.LessEqual,
            });
    }

    public unsafe void DrawWireBox(ID3D11DeviceContext context, in Matrix4x4 view, in Matrix4x4 proj, in Vector3 worldMin, in Vector3 worldMax, in Color4 color)
    {
        float sx = (worldMax.X - worldMin.X) * 0.5f;
        float sy = (worldMax.Y - worldMin.Y) * 0.5f;
        float sz = (worldMax.Z - worldMin.Z) * 0.5f;
        var center = (worldMin + worldMax) * 0.5f;
        Matrix4x4 world = Matrix4x4.Multiply(Matrix4x4.CreateScale(sx, sy, sz), Matrix4x4.CreateTranslation(center));
        Matrix4x4 wvp = Matrix4x4.Multiply(Matrix4x4.Multiply(world, view), proj);

        MappedSubresource mapped = context.Map(_constantBuffer, MapMode.WriteDiscard);
        var data = (LineConstants*)mapped.DataPointer;
        data->WorldViewProjection = wvp;
        data->Color = color;
        context.Unmap(_constantBuffer, 0);

        context.IASetPrimitiveTopology(PrimitiveTopology.LineList);
        context.IASetInputLayout(_il);
        context.IASetVertexBuffer(0, _vertexBuffer, Stride);
        context.IASetIndexBuffer(_indexBuffer, Format.R32_UInt, 0);
        context.VSSetShader(_vs);
        context.PSSetShader(_ps);
        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetConstantBuffer(0, _constantBuffer);
        context.RSSetState(_rs);
        context.OMSetDepthStencilState(_depth, 0);
        context.OMSetBlendState(null, null, uint.MaxValue);

        context.DrawIndexed(24, 0, 0);
    }

    public void Dispose()
    {
        _depth.Dispose();
        _rs.Dispose();
        _il.Dispose();
        _ps.Dispose();
        _vs.Dispose();
        _constantBuffer.Dispose();
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LineConstants
    {
        public Matrix4x4 WorldViewProjection;
        public Color4 Color;
    }
}
