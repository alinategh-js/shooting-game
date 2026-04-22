using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace ShootingEngine.Graphics;

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
            DrawOne(context, in inst.World, in inst.Tint, in view, in proj);
        }

        if (alphaOverlays is { Count: > 0 })
        {
            context.OMSetDepthStencilState(_depthReadOnlyState, 0);
            context.OMSetBlendState(_alphaBlendState, new Color4(0, 0, 0, 0), uint.MaxValue);
            for (int i = 0; i < alphaOverlays.Count; i++)
            {
                SceneInstance inst = alphaOverlays[i];
                DrawOne(context, in inst.World, in inst.Tint, in view, in proj);
            }

            context.OMSetBlendState(null, null, uint.MaxValue);
            context.OMSetDepthStencilState(_depthStencilState, 0);
        }

        // Hand/viewmodel
        context.RSSetState(_handRasterizerState);
        context.OMSetDepthStencilState(_handDepthStencilState, 0);
        for (int i = 0; i < handInstances.Count; i++)
        {
            SceneInstance inst = handInstances[i];
            DrawOne(context, in inst.World, in inst.Tint, in view, in proj);
        }
    }

    private unsafe void DrawOne(ID3D11DeviceContext context, in Matrix4x4 world, in Color4 tint, in Matrix4x4 view, in Matrix4x4 proj)
    {
        Matrix4x4 wvp = world * view * proj;
        var cb = new PerDrawConstants(wvp, tint);

        MappedSubresource mapped = context.Map(_constantBuffer, MapMode.WriteDiscard);
        Unsafe.WriteUnaligned(mapped.DataPointer.ToPointer(), cb);
        context.Unmap(_constantBuffer, 0);

        context.VSSetConstantBuffer(0, _constantBuffer);
        context.PSSetConstantBuffer(0, _constantBuffer);
        context.DrawIndexed(CubeIndexCount, 0, 0);
    }

    public void Dispose()
    {
        _alphaBlendState.Dispose();
        _depthReadOnlyState.Dispose();
        _handDepthStencilState.Dispose();
        _depthStencilState.Dispose();
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
    private readonly struct PerDrawConstants
    {
        public readonly Matrix4x4 WorldViewProj;
        public readonly Color4 Tint;

        public PerDrawConstants(Matrix4x4 worldViewProj, Color4 tint)
        {
            WorldViewProj = worldViewProj;
            Tint = tint;
        }
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

