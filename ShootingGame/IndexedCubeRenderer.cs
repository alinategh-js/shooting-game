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
    private const int CubeIndexCount = 36;

    private readonly ID3D11Buffer _vertexBuffer;
    private readonly ID3D11Buffer _indexBuffer;
    private readonly ID3D11Buffer _constantBuffer;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly ID3D11InputLayout _inputLayout;
    private readonly ID3D11RasterizerState _rasterizerState;
    private readonly ID3D11RasterizerState _handRasterizerState;
    private readonly ID3D11DepthStencilState _depthStencilState;
    private readonly ID3D11DepthStencilState _handDepthStencilState;
    private readonly ID3D11DepthStencilState _depthReadOnlyState;
    private readonly ID3D11BlendState _alphaBlendState;

    public IndexedCubeRenderer(ID3D11Device device)
    {
        var white = new Color4(1f, 1f, 1f, 1f);
        // 24 verts (4 per face) for crisp flat lighting (face normals).
        ReadOnlySpan<CubeVertex> vertices =
        [
            // -Z
            new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), white),
            new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0, 0, -1), white),
            new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0, 0, -1), white),
            new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0, 0, -1), white),
            // +Z
            new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0, 0, 1), white),
            new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0, 0, 1), white),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 0, 1), white),
            new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0, 0, 1), white),
            // -X
            new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-1, 0, 0), white),
            new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-1, 0, 0), white),
            new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-1, 0, 0), white),
            new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-1, 0, 0), white),
            // +X
            new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(1, 0, 0), white),
            new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(1, 0, 0), white),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1, 0, 0), white),
            new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(1, 0, 0), white),
            // -Y
            new(new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0, -1, 0), white),
            new(new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0, -1, 0), white),
            new(new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0, -1, 0), white),
            new(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0, -1, 0), white),
            // +Y
            new(new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0, 1, 0), white),
            new(new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0, 1, 0), white),
            new(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0, 1, 0), white),
            new(new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0, 1, 0), white),
        ];

        ReadOnlySpan<uint> indices =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            8, 9, 10, 8, 10, 11,
            12, 13, 14, 12, 14, 15,
            16, 17, 18, 16, 18, 19,
            20, 21, 22, 20, 22, 23,
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
            new("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new("COLOR", 0, Format.R32G32B32A32_Float, 24, 0),
        ];

        _inputLayout = device.CreateInputLayout(inputElements, vsBytecode.Span);

        _rasterizerState = device.CreateRasterizerState(
            new RasterizerDescription
            {
                CullMode = CullMode.Back,
                FillMode = FillMode.Solid,
            });

        // Viewmodel is very close to the eye; disable culling so back faces never hide the whole hand.
        _handRasterizerState = device.CreateRasterizerState(
            new RasterizerDescription
            {
                CullMode = CullMode.None,
                FillMode = FillMode.Solid,
            });

        _depthStencilState = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.Less,
            });

        // Viewmodel: render on top (no depth test) to avoid clipping / z-fighting glitches.
        _handDepthStencilState = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = false,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthFunc = ComparisonFunction.Always,
            });

        _depthReadOnlyState = device.CreateDepthStencilState(
            new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.Zero,
                DepthFunc = ComparisonFunction.LessEqual,
            });

        _alphaBlendState = device.CreateBlendState(BlendDescription.AlphaBlend);
    }

    public unsafe void Draw(
        ID3D11DeviceContext context,
        int width,
        int height,
        float simulationTimeSeconds,
        in Matrix4x4 view,
        List<SceneInstance> instances,
        List<SceneInstance> handInstances,
        List<SceneInstance>? alphaOverlays = null,
        float farClipDistance = 100f)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        float aspect = width / (float)height;
        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.05f, farClipDistance);

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

        if (alphaOverlays is { Count: > 0 })
        {
            context.OMSetDepthStencilState(_depthReadOnlyState, 0);
            context.OMSetBlendState(_alphaBlendState, new Color4(0, 0, 0, 0), uint.MaxValue);
            for (int i = 0; i < alphaOverlays.Count; i++)
            {
                SceneInstance a = alphaOverlays[i];
                DrawOne(context, in a.World, in view, in proj, in a.Tint);
            }

            context.OMSetBlendState(null, null, uint.MaxValue);
            context.OMSetDepthStencilState(_depthStencilState, 0);
        }

        context.OMSetDepthStencilState(_handDepthStencilState, 0);
        context.RSSetState(_handRasterizerState);
        for (int i = 0; i < handInstances.Count; i++)
        {
            SceneInstance h = handInstances[i];
            DrawOne(context, in h.World, in view, in proj, in h.Tint);
        }
        context.RSSetState(_rasterizerState);
        context.OMSetDepthStencilState(_depthStencilState, 0);
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
        data->World = world;
        data->WorldViewProjection = wvp;
        data->Tint = tint;
        context.Unmap(_constantBuffer, 0);

        context.VSSetConstantBuffer(0, _constantBuffer);
        context.DrawIndexed(CubeIndexCount, 0, 0);
    }

    public void Dispose()
    {
        _alphaBlendState.Dispose();
        _depthReadOnlyState.Dispose();
        _depthStencilState.Dispose();
        _handDepthStencilState.Dispose();
        _handRasterizerState.Dispose();
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
        public Matrix4x4 World;
        public Matrix4x4 WorldViewProjection;
        public Color4 Tint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CubeVertex
    {
        public readonly Vector3 Position;
        public readonly Vector3 Normal;
        public readonly Color4 Color;

        public CubeVertex(Vector3 position, Vector3 normal, Color4 color)
        {
            Position = position;
            Normal = normal;
            Color = color;
        }
    }
}
