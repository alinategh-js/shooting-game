using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace ShootingGame;

/// <summary>
/// Owns the D3D11 device, swap chain, back-buffer RTV, and a matching depth buffer.
/// </summary>
public sealed class D3D11Host : IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_0,
    ];

    private const int SwapChainBufferCount = 2;
    private const Format ColorFormat = Format.B8G8R8A8_UNorm;
    private const Format DepthFormat = Format.D24_UNorm_S8_UInt;

    private readonly IDXGIFactory2 _factory;
    private readonly IntPtr _hwnd;

    private IDXGISwapChain1 _swapChain;
    private ID3D11Texture2D _backBuffer = null!;
    private ID3D11RenderTargetView _rtv = null!;
    private ID3D11Texture2D _depth = null!;
    private ID3D11DepthStencilView _dsv = null!;

    public D3D11Host(IntPtr hwnd, int width, int height)
    {
        _hwnd = hwnd;
        _factory = CreateDXGIFactory1<IDXGIFactory2>();

        D3D11CreateDevice(
                null,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                FeatureLevels,
                out var device,
                out _,
                out var context)
            .CheckError();

        Device = device;
        Context = context;

        _swapChain = CreateSwapChain(width, height);
        _factory.MakeWindowAssociation(_hwnd, WindowAssociationFlags.IgnoreAltEnter);

        CreateSizeDependentResources(width, height);
    }

    public ID3D11Device Device { get; }

    public ID3D11DeviceContext Context { get; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (width == Width && height == Height)
        {
            return;
        }

        ReleaseSizeDependentResources();

        _swapChain.ResizeBuffers(SwapChainBufferCount, (uint)width, (uint)height, ColorFormat, SwapChainFlags.None)
            .CheckError();

        Width = width;
        Height = height;
        CreateSizeDependentResources(width, height);
    }

    public void RenderFrame(Color4 clearColor, Action<ID3D11DeviceContext>? draw = null)
    {
        Context.ClearRenderTargetView(_rtv, clearColor);
        Context.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);

        Context.OMSetRenderTargets(_rtv, _dsv);
        Context.RSSetViewport(new Viewport(0, 0, Width, Height, 0f, 1f));

        draw?.Invoke(Context);

        _ = _swapChain.Present(1, PresentFlags.None);
    }

    public void Dispose()
    {
        ReleaseSizeDependentResources();
        _swapChain.Dispose();
        Context.Dispose();
        Device.Dispose();
        _factory.Dispose();
    }

    private IDXGISwapChain1 CreateSwapChain(int width, int height)
    {
        var desc = new SwapChainDescription1
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = ColorFormat,
            Stereo = false,
            SampleDescription = SampleDescription.Default,
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = SwapChainBufferCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
        };

        var fullscreenDesc = new SwapChainFullscreenDescription
        {
            Windowed = true,
        };

        Width = width;
        Height = height;

        return _factory.CreateSwapChainForHwnd(Device, _hwnd, desc, fullscreenDesc);
    }

    private void CreateSizeDependentResources(int width, int height)
    {
        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _rtv = Device.CreateRenderTargetView(_backBuffer);

        _depth = Device.CreateTexture2D(
            DepthFormat,
            (uint)width,
            (uint)height,
            mipLevels: 1,
            bindFlags: BindFlags.DepthStencil);

        _dsv = Device.CreateDepthStencilView(
            _depth,
            new DepthStencilViewDescription(_depth, DepthStencilViewDimension.Texture2D));
    }

    private void ReleaseSizeDependentResources()
    {
        _dsv.Dispose();
        _depth.Dispose();
        _rtv.Dispose();
        _backBuffer.Dispose();
    }
}
