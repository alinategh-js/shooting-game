using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ShootingGame;

/// <summary>
/// Unit cube mesh with per-draw WVP + tint (all scene geometry is scaled/translated cubes).
/// </summary>
public sealed class IndexedCubeRenderer : IDisposable
{
    private static readonly uint VertexStride = (uint)Unsafe.SizeOf<CubeVertex>();

    private readonly ID3D11Buffer _vertexBuffer;
    private readonly ID3D11Buffer _indexBuffer;
    private readonly ID3D11Buffer _constantBuffer;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11InputLayout _inputLayout;
    private readonly ID3D11RasterizerState _rasterizerState;
    private readonly ID3D11DepthStencilState _depthStencilState;

    public IndexedCubeRenderer(ID3D11Device device)
    {
        var white = new Color4(1f, 1f, 1f, 1f);
        ReadOnlySpan<CubeVertex> vertices =
        [
            new(new Vector3(-0.5f, -0.5f, -0.5f), white),
            new(new Vector3(0.5f, -0.5f, -0.5f), white),
            new(new Vector3(0.5f, 0.5f, -0.5f), white),
            new(new Vector3(-0.5f, 0.5f, -0.5f), white),
            new(new Vector3(-0.5f, -0.5f, 0.5f), white),
            new(new Vector3(0.5f, -0.5f, 0.5f), white),
            new(new Vector3(0.5f, 0.5f, 0.5f), white),
            new(new Vector3(-0.5f, 0.5f, 0.5f), white),
        ];

        ReadOnlySpan<uint> indices =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            2, 6, 7, 2, 7, 3,
            0, 3, 7, 0, 7, 4,
            1, 5, 6, 1, 6, 2,
        ];

        _vertexBuffer = device.CreateBuffer(vertices, BindFlags.VertexBuffer);
        _indexBuffer = device.CreateBuffer(indices, BindFlags.IndexBuffer);

        uint cbSize = (uint)Unsafe.SizeOf<PerDrawConstants>();
        _constantBuffer = device.CreateBuffer(
            cbSize,
            BindFlags.ConstantBuffer,
            ResourceUsage.Dynamic,
            CpuAccessFlags.Write);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "Basic.hlsl");
        ReadOnlyMemory<byte> vsBytecode = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_5_0");
        ReadOnlyMemory<byte> psBytecode = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_5_0");

        _vertexShader = device.CreateVertexShader(vsBytecode.Span);
        _pixelShader = device.CreatePixelShader(psBytecode.Span);

        InputElementDescription[] inputElements =
        [
            new("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
        ];

        _inputLayout = device.CreateInputLayout(inputElements, vsBytecode.Span);

        _rasterizerState = device.CreateRasterizerState(
            new RasterizerDescription
            {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
            });

        _depthStencilState = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.Less,
            });
    }

    public unsafe void Draw(
        ID3D11DeviceContext context,
        int width,
        int height,
        float simulationTimeSeconds,
        in Matrix4x4 view,
        List<SceneInstance> instances,
        in Matrix4x4 handWorld,
        in Color4 handTint)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        float aspect = width / (float)height;
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.1f, 100f);

        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(_inputLayout);
        context.IASetVertexBuffer(0, _vertexBuffer, VertexStride);
        context.IASetIndexBuffer(_indexBuffer, Format.R32_UInt, 0);

        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);

        context.RSSetState(_rasterizerState);
        context.OMSetDepthStencilState(_depthStencilState, 0);
        context.OMSetBlendState(null, null, uint.MaxValue);

        for (int i = 0; i < instances.Count; i++)
        {
            SceneInstance inst = instances[i];
            DrawOne(context, in inst.World, in view, in proj, in inst.Tint);
        }

        float warmth = 0.92f + 0.08f * MathF.Sin(simulationTimeSeconds * 3.1f);
        var handFinal = new Color4(handTint.R * warmth, handTint.G * warmth, handTint.B * warmth, handTint.A);
        DrawOne(context, in handWorld, in view, in proj, in handFinal);
    }

    private unsafe void DrawOne(
        ID3D11DeviceContext context,
        in Matrix4x4 world,
        in Matrix4x4 view,
        in Matrix4x4 proj,
        in Color4 tint)
    {
        Matrix4x4 wvp = Matrix4x4.Multiply(Matrix4x4.Multiply(world, view), proj);

        MappedSubresource mapped = context.Map(_constantBuffer, MapMode.WriteDiscard);
        var data = (PerDrawConstants*)mapped.DataPointer;
        data->WorldViewProjection = wvp;
        data->Tint = tint;
        context.Unmap(_constantBuffer, 0);

        context.VSSetConstantBuffer(0, _constantBuffer);
        context.DrawIndexed(36, 0, 0);
    }

    public void Dispose()
    {
        _depthStencilState.Dispose();
        _rasterizerState.Dispose();
        _inputLayout.Dispose();
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        _constantBuffer.Dispose();
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerDrawConstants
    {
        public Matrix4x4 WorldViewProjection;
        public Color4 Tint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CubeVertex
    {
        public readonly Vector3 Position;
        public readonly float Pad;
        public readonly Color4 Color;

        public CubeVertex(Vector3 position, Color4 color)
        {
            Position = position;
            Pad = 0f;
            Color = color;
        }
    }
}
