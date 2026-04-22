using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Hexa.NET.SDL2;
using ShootingEngine.Core;
using ShootingEngine.Graphics;
using Vortice.Mathematics;

namespace ShootingGame;

/// <summary>
/// SDL2 window + main loop: pump events, measure frame time, drive D3D11 clear/present.
/// </summary>
public static class GameApp
{
    private const int InitialWidth = 1280;
    private const int InitialHeight = 720;

    // SDL_WINDOWPOS_CENTERED (see SDL_video.h)
    private static readonly int WindowPosCentered = unchecked((int)SDL.SDL_WINDOWPOS_CENTERED_MASK);

    private static string GetSdlErrorString()
        => SDL.GetErrorAsException()?.Message ?? string.Empty;

    public static void Run()
    {
        SDL.SetHint(SDL.SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");

        if (SDL.Init(SDL.SDL_INIT_VIDEO | SDL.SDL_INIT_EVENTS) != 0)
        {
            throw new InvalidOperationException($"SDL_Init failed: {GetSdlErrorString()}");
        }

        try
        {
            unsafe
            {
                SDLWindow* window = SDL.CreateWindow(
                    "ShootingGame",
                    WindowPosCentered,
                    WindowPosCentered,
                    InitialWidth,
                    InitialHeight,
                    (uint)(SDLWindowFlags.Resizable | SDLWindowFlags.Shown));

                if (window == null)
                {
                    throw new InvalidOperationException($"SDL_CreateWindow failed: {GetSdlErrorString()}");
                }

                try
                {
                    nint hwnd = GetWin32Hwnd(window);
                    using var gpu = new D3D11Host(hwnd, InitialWidth, InitialHeight);
                    using var cube = new IndexedCubeRenderer(gpu.Device);

                    uint windowId = SDL.GetWindowID(window);
                    var clock = Stopwatch.StartNew();
                    double previousSeconds = 0;
                    var gameTime = new GameTime();

                    var camera = FpsCamera.CreateDefault();
                    var relativeMouse = true;
                    SDL.SetRelativeMouseMode(SDLBool.True);
                    var sceneInstances = new List<SceneInstance>(96);
                    var grounded = true;
                    var handInstances = new List<SceneInstance>(32);

                    SDLEvent evt = default;
                    var running = true;

                    while (running)
                    {
                        int mouseDx = 0;
                        int mouseDy = 0;

                        SDL.PumpEvents();
                        while (SDL.PollEvent(ref evt) != 0)
                        {
                            switch ((SDLEventType)evt.Type)
                            {
                                case SDLEventType.Quit:
                                    running = false;
                                    break;

                                case SDLEventType.AppTerminating:
                                    running = false;
                                    break;

                                case SDLEventType.Keydown:
                                {
                                    ref readonly var key = ref evt.Key;
                                    if (key.WindowID == windowId
                                        && key.Repeat == 0
                                        && key.Keysym.Scancode == SDLScancode.Escape)
                                    {
                                        relativeMouse = !relativeMouse;
                                        SDL.SetRelativeMouseMode(relativeMouse ? SDLBool.True : SDLBool.False);
                                    }

                                    break;
                                }

                                case SDLEventType.Mousemotion:
                                {
                                    ref readonly var motion = ref evt.Motion;
                                    if (motion.WindowID == windowId)
                                    {
                                        mouseDx += motion.Xrel;
                                        mouseDy += motion.Yrel;
                                    }

                                    break;
                                }

                                case SDLEventType.Windowevent:
                                    ref readonly var we = ref evt.Window;
                                    if (we.WindowID == windowId)
                                    {
                                        var wid = (SDLWindowEventID)we.Event;
                                        if (wid == SDLWindowEventID.Close)
                                        {
                                            running = false;
                                        }
                                        else if (wid is SDLWindowEventID.Resized or SDLWindowEventID.SizeChanged)
                                        {
                                            int w = we.Data1;
                                            int h = we.Data2;
                                            gpu.Resize(w, h);
                                        }
                                    }

                                    break;
                            }
                        }

                        double t = clock.Elapsed.TotalSeconds;
                        double dt = t - previousSeconds;
                        previousSeconds = t;

                        gameTime.AdvanceSimulation(dt);

                        int ww, wh;
                        SDL.GetWindowSize(window, &ww, &wh);
                        if (ww != gpu.Width || wh != gpu.Height)
                        {
                            gpu.Resize(ww, wh);
                        }

                        int clientW = ww;
                        int clientH = wh;

                        if (relativeMouse)
                        {
                            camera.ApplyMouseLook(mouseDx, mouseDy);
                        }

                        int keyCountRaw = 0;
                        byte* keys = SDL.GetKeyboardState(&keyCountRaw);
                        int keyCount = keyCountRaw;
                        camera.UpdateLocomotion((float)dt, keys, keyCount, SceneLevel.Colliders, ref grounded);

                        Matrix4x4 view = camera.GetViewMatrix();

                        double fps = dt > 1e-6 ? 1.0 / dt : 0;
                        double ms = dt * 1000.0;
                        string mode = relativeMouse ? "look" : "ui";
                        string gnd = grounded ? "ground" : "air";
                        string cr = camera.IsCrouching ? "crouch" : "stand";
                        string title =
                            $"ShootingGame | {fps:0} fps | {ms:0.###} ms | {ww}x{wh} | sim {gameTime.SimulationTimeSeconds:0.00}s | {gnd} | {cr} | {mode} (Esc)";
                        SDL.SetWindowTitle(window, title);

                        float skyPulse = 0.5f + 0.5f * MathF.Sin((float)(t * 0.55));
                        var clear = new Color4(
                            0.06f + 0.02f * skyPulse,
                            0.09f + 0.03f * skyPulse,
                            0.16f + 0.04f * skyPulse,
                            1f);
                        float sim = gameTime.SimulationTimeSeconds;
                        sceneInstances.Clear();
                        SceneLevel.AppendDrawInstances(sim, sceneInstances);
                        handInstances.Clear();
                        camera.AppendRightHandInstances(handInstances, sim);
                        gpu.RenderFrame(clear, ctx => cube.Draw(ctx, clientW, clientH, sim, view, sceneInstances, handInstances));
                    }
                }
                finally
                {
                    SDL.SetRelativeMouseMode(SDLBool.False);
                    SDL.DestroyWindow(window);
                }
            }
        }
        finally
        {
            SDL.Quit();
        }
    }

    private static unsafe nint GetWin32Hwnd(SDLWindow* window)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("This bootstrap path is Windows-first (HWND + D3D11).");
        }

        var wmInfo = new SDLSysWMInfo();
        SDL.GetVersion(ref wmInfo.Version);

        if (SDL.GetWindowWMInfo(window, ref wmInfo) != SDLBool.True)
        {
            throw new InvalidOperationException($"SDL_GetWindowWMInfo failed: {GetSdlErrorString()}");
        }

        if (wmInfo.Subsystem != SdlSyswmType.Windows)
        {
            throw new InvalidOperationException($"Expected Win32 subsystem, got {wmInfo.Subsystem}.");
        }

        return wmInfo.Info.Win.Window;
    }
}
