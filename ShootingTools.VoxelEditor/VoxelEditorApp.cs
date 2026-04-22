using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.SDL2;
using ShootingEngine.Cameras;
using ShootingEngine.Graphics;
using ShootingEngine.Voxel;
using Vortice.Mathematics;

namespace ShootingTools.VoxelEditor;

/// <summary>
/// Developer voxel object editor.
/// </summary>
public static class VoxelEditorApp
{
    private const int InitialWidth = 1280;
    private const int InitialHeight = 720;
    private const int VoxelSize = 128;
    private const float CellSize = 0.05f;
    private const float CameraFarClip = 200f;

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
                    "Voxel Editor (developer)",
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
                    using var lineBox = new LinePrimitiveRenderer(gpu.Device);
                    using var ui = new UiTriangleRenderer(gpu.Device);

                    uint windowId = SDL.GetWindowID(window);
                    var clock = Stopwatch.StartNew();
                    double previousSeconds = 0;

                    var volume = new DenseRgbVoxelVolume(VoxelSize, VoxelSize, VoxelSize);
                    volume.Clear(VoxelRgbA.Air);

                    var baker = new VoxelMeshBaker();
                    var instances = new List<SceneInstance>(4096);
                    var ghostInstances = new List<SceneInstance>(4);
                    var handEmpty = new List<SceneInstance>(1);
                    var uiVerts = new List<UiVertex>(2048);
                    Color4[] palette = EditorUiBuilder.BuildDefaultPalette();

                    baker.Rebuild(volume, CellSize, instances);

                    int mouseX = 0;
                    int mouseY = 0;
                    bool lmbHeld = false;
                    byte cr = (byte)(palette[0].R * 255f);
                    byte cg = (byte)(palette[0].G * 255f);
                    byte cb = (byte)(palette[0].B * 255f);
                    int activePaletteIndex = 0;
                    int hoverPaletteIndex = -1;
                    VoxelEditorToolKind tool = VoxelEditorToolKind.Draw;
                    VoxelEditorToolKind hoverTool = VoxelEditorToolKind.Draw;
                    int channel = 0;

                    string exportDir = Path.Combine(AppContext.BaseDirectory, "VoxelExports");
                    Directory.CreateDirectory(exportDir);
                    string activePath = Path.Combine(exportDir, "object.svxv");

                    bool meshDirty = true;
                    bool relativeMouse = false;
                    SDL.SetRelativeMouseMode(SDLBool.False);

                    float extent = VoxelSize * 0.5f * CellSize;
                    var worldMin = new Vector3(-extent, -extent, -extent);
                    var worldMax = new Vector3(extent, extent, extent);

                    var cam = FlyCamera.CreateDefault(new Vector3(0f, -extent * 0.35f, -extent * 1.6f));
                    cam.Yaw = 0.6f;
                    cam.Pitch = 0.15f;

                    SDLEvent evt = default;
                    var running = true;

                    while (running)
                    {
                        double t = clock.Elapsed.TotalSeconds;
                        double dt = t - previousSeconds;
                        previousSeconds = t;

                        int mouseDx = 0;
                        int mouseDy = 0;

                        SDL.PumpEvents();
                        while (SDL.PollEvent(ref evt) != 0)
                        {
                            switch ((SDLEventType)evt.Type)
                            {
                                case SDLEventType.Quit:
                                case SDLEventType.AppTerminating:
                                    running = false;
                                    break;

                                case SDLEventType.Keydown:
                                {
                                    ref readonly var key = ref evt.Key;
                                    if (key.WindowID == windowId && key.Repeat == 0)
                                    {
                                        var sc = key.Keysym.Scancode;
                                        if (sc == SDLScancode.Escape)
                                        {
                                            relativeMouse = !relativeMouse;
                                            SDL.SetRelativeMouseMode(relativeMouse ? SDLBool.True : SDLBool.False);
                                        }

                                        if (sc == SDLScancode.Tab)
                                        {
                                            channel = (channel + 1) % 3;
                                        }

                                        int n = 0;
                                        byte* k = SDL.GetKeyboardState(&n);
                                        bool ctrl = IsDown(k, n, SDLScancode.Lctrl) || IsDown(k, n, SDLScancode.Rctrl);

                                        if (sc == SDLScancode.S && ctrl)
                                        {
                                            SaveVolume(volume, activePath);
                                        }

                                        if (sc == SDLScancode.O && ctrl)
                                        {
                                            volume = TryLoadVolume(activePath);
                                            meshDirty = true;
                                        }

                                        if (sc == SDLScancode.F5)
                                        {
                                            SaveVolume(volume, activePath);
                                        }

                                        if (sc == SDLScancode.F9)
                                        {
                                            volume = TryLoadVolume(activePath);
                                            meshDirty = true;
                                        }
                                    }

                                    break;
                                }

                                case SDLEventType.Mousebuttondown:
                                {
                                    ref readonly var b = ref evt.Button;
                                    if (b.WindowID == windowId)
                                    {
                                        uint btn = (uint)b.Button;
                                        if (btn == 1u)
                                        {
                                            lmbHeld = true;
                                            int winW, winH;
                                            SDL.GetWindowSize(window, &winW, &winH);
                                            if (EditorUiBuilder.HitPalette(b.X, b.Y, out int pi))
                                            {
                                                activePaletteIndex = pi;
                                                var pc = palette[pi];
                                                cr = (byte)(pc.R * 255f);
                                                cg = (byte)(pc.G * 255f);
                                                cb = (byte)(pc.B * 255f);
                                            }
                                            else if (EditorUiBuilder.HitDrawTool(winW, b.X, b.Y))
                                            {
                                                tool = VoxelEditorToolKind.Draw;
                                            }
                                            else if (EditorUiBuilder.HitEraseTool(winW, b.X, b.Y))
                                            {
                                                tool = VoxelEditorToolKind.Erase;
                                            }
                                        }
                                        else if (btn == 3u)
                                        {
                                            // RMB reserved (no-op for now)
                                        }
                                    }

                                    break;
                                }

                                case SDLEventType.Mousebuttonup:
                                {
                                    ref readonly var b = ref evt.Button;
                                    if (b.WindowID == windowId)
                                    {
                                        uint btn = (uint)b.Button;
                                        if (btn == 1u)
                                        {
                                            lmbHeld = false;
                                        }
                                        else if (btn == 3u)
                                        {
                                            // RMB reserved (no-op for now)
                                        }
                                    }

                                    break;
                                }

                                case SDLEventType.Mousemotion:
                                {
                                    ref readonly var m = ref evt.Motion;
                                    if (m.WindowID == windowId)
                                    {
                                        mouseX = m.X;
                                        mouseY = m.Y;

                                        if (relativeMouse)
                                        {
                                            mouseDx += m.Xrel;
                                            mouseDy += m.Yrel;
                                        }
                                    }

                                    break;
                                }

                                case SDLEventType.Mousewheel:
                                {
                                    ref readonly var w = ref evt.Wheel;
                                    if (w.WindowID == windowId)
                                    {
                                        float dy = w.PreciseY != 0f ? w.PreciseY : w.Y;
                                        cam.BaseSpeed *= MathF.Exp(dy * 0.08f);
                                        cam.BaseSpeed = Math.Clamp(cam.BaseSpeed, 0.4f, 50f);
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
                                            gpu.Resize(we.Data1, we.Data2);
                                        }
                                    }

                                    break;
                            }
                        }

                        int keyCountRaw = 0;
                        byte* keys = SDL.GetKeyboardState(&keyCountRaw);
                        int keyCount = keyCountRaw;
                        AdjustColorFromKeys(keys, keyCount, ref channel, ref cr, ref cg, ref cb);

                        cam.UpdateMovement((float)dt, keys, keyCount);
                        if (relativeMouse)
                        {
                            cam.ApplyMouseLook(mouseDx, mouseDy);
                        }

                        int ww, wh;
                        SDL.GetWindowSize(window, &ww, &wh);
                        if (ww != gpu.Width || wh != gpu.Height)
                        {
                            gpu.Resize(ww, wh);
                        }

                        Matrix4x4 view = cam.GetViewMatrix();
                        float aspect = ww / (float)Math.Max(1, wh);
                        Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.05f, CameraFarClip);

                        hoverPaletteIndex = -1;
                        hoverTool = tool;
                        if (EditorUiBuilder.HitPalette(mouseX, mouseY, out int hi))
                        {
                            hoverPaletteIndex = hi;
                        }
                        else if (EditorUiBuilder.HitDrawTool(ww, mouseX, mouseY))
                        {
                            hoverTool = VoxelEditorToolKind.Draw;
                        }
                        else if (EditorUiBuilder.HitEraseTool(ww, mouseX, mouseY))
                        {
                            hoverTool = VoxelEditorToolKind.Erase;
                        }

                        ghostInstances.Clear();
                        if (EditorUiBuilder.IsIn3DViewport(ww, mouseX, mouseY)
                            && TryPickRay(mouseX, mouseY, ww, wh, in view, in proj, out Vector3 ro, out Vector3 rd))
                        {
                            if (tool == VoxelEditorToolKind.Draw
                                && VoxelPlacementRay.TryGetDrawPlacement(volume, ro, rd, CellSize, out int gx, out int gy, out int gz))
                            {
                                Vector3 c = VoxelMeshBaker.CellCenterWorld(gx, gy, gz, VoxelSize, VoxelSize, VoxelSize, CellSize);
                                Matrix4x4 w = Matrix4x4.Multiply(
                                    Matrix4x4.CreateScale(CellSize, CellSize, CellSize),
                                    Matrix4x4.CreateTranslation(c));
                                float inv = 1f / 255f;
                                ghostInstances.Add(new SceneInstance(
                                    w,
                                    new Color4(cr * inv, cg * inv, cb * inv, 0.28f)));
                            }
                            else if (tool == VoxelEditorToolKind.Erase
                                && VoxelGridRay.TryFirstSolid(volume, ro, rd, CellSize, out int ex, out int ey, out int ez))
                            {
                                Vector3 c = VoxelMeshBaker.CellCenterWorld(ex, ey, ez, VoxelSize, VoxelSize, VoxelSize, CellSize);
                                Matrix4x4 w = Matrix4x4.Multiply(
                                    Matrix4x4.CreateScale(CellSize, CellSize, CellSize),
                                    Matrix4x4.CreateTranslation(c));
                                ghostInstances.Add(new SceneInstance(w, new Color4(1f, 0.25f, 0.2f, 0.22f)));
                            }
                        }

                        if (lmbHeld && EditorUiBuilder.IsIn3DViewport(ww, mouseX, mouseY)
                            && TryPickRay(mouseX, mouseY, ww, wh, in view, in proj, out ro, out rd))
                        {
                            if (tool == VoxelEditorToolKind.Draw
                                && VoxelPlacementRay.TryGetDrawPlacement(volume, ro, rd, CellSize, out int px, out int py, out int pz))
                            {
                                volume.Set(px, py, pz, new VoxelRgbA(cr, cg, cb, 255));
                                meshDirty = true;
                            }
                            else if (tool == VoxelEditorToolKind.Erase
                                && VoxelGridRay.TryFirstSolid(volume, ro, rd, CellSize, out int sx, out int sy, out int sz))
                            {
                                volume.Set(sx, sy, sz, VoxelRgbA.Air);
                                meshDirty = true;
                            }
                        }

                        if (meshDirty)
                        {
                            baker.Rebuild(volume, CellSize, instances);
                            meshDirty = false;
                        }

                        EditorUiBuilder.BuildUi(
                            ww,
                            wh,
                            mouseX,
                            mouseY,
                            palette,
                            activePaletteIndex,
                            hoverPaletteIndex,
                            tool,
                            hoverTool,
                            new Color4(cr / 255f, cg / 255f, cb / 255f, 1f),
                            uiVerts);

                        string ch = channel == 0 ? "R" : channel == 1 ? "G" : "B";
                        string warn = baker.TruncatedLastBuild ? " | TRUNCATED_PREVIEW" : string.Empty;
                        string title = new StringBuilder(320)
                            .Append("VoxelEditor | ")
                            .Append(ww).Append('x').Append(wh)
                            .Append(" | ").Append(tool)
                            .Append(" | RGB(").Append(cr).Append(',').Append(cg).Append(',').Append(cb).Append(')')
                            .Append(" | ch=").Append(ch)
                            .Append(" | Ctrl+S/O save/load | F5/F9 | Esc mouse | Tab ch | WASD/QE move")
                            .Append(warn)
                            .ToString();
                        SDL.SetWindowTitle(window, title);

                        var clear = new Color4(0.05f, 0.06f, 0.09f, 1f);
                        int clientW = ww;
                        int clientH = wh;
                        gpu.RenderFrame(clear, ctx =>
                        {
                            cube.Draw(ctx, clientW, clientH, (float)t, view, instances, handEmpty, ghostInstances, CameraFarClip);
                            lineBox.DrawWireBox(ctx, in view, in proj, in worldMin, in worldMax, new Color4(1f, 1f, 1f, 0.55f));
                            ui.Draw(ctx, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(uiVerts));
                        });
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

    private static unsafe bool IsDown(byte* keys, int keyCount, SDLScancode sc)
    {
        int i = (int)sc;
        return i >= 0 && i < keyCount && keys[i] != 0;
    }

    private static unsafe void AdjustColorFromKeys(byte* keys, int keyCount, ref int channel, ref byte cr, ref byte cg, ref byte cb)
    {
        int step = IsDown(keys, keyCount, SDLScancode.Lshift) || IsDown(keys, keyCount, SDLScancode.Rshift) ? 15 : 3;

        void Up(ref byte b) => b = (byte)Math.Clamp((int)b + step, 0, 255);
        void Down(ref byte b) => b = (byte)Math.Clamp((int)b - step, 0, 255);

        if (IsDown(keys, keyCount, SDLScancode.Up))
        {
            if (channel == 0)
            {
                Up(ref cr);
            }
            else if (channel == 1)
            {
                Up(ref cg);
            }
            else
            {
                Up(ref cb);
            }
        }

        if (IsDown(keys, keyCount, SDLScancode.Down))
        {
            if (channel == 0)
            {
                Down(ref cr);
            }
            else if (channel == 1)
            {
                Down(ref cg);
            }
            else
            {
                Down(ref cb);
            }
        }
    }

    private static bool TryPickRay(int mx, int my, int w, int h, in Matrix4x4 view, in Matrix4x4 proj, out Vector3 origin, out Vector3 dir)
    {
        origin = default;
        dir = default;
        if (w <= 0 || h <= 0)
        {
            return false;
        }

        float ndcX = (mx / (float)w) * 2f - 1f;
        float ndcY = 1f - (my / (float)h) * 2f;

        Matrix4x4 vp = Matrix4x4.Multiply(view, proj);
        if (!Matrix4x4.Invert(vp, out Matrix4x4 invVp))
        {
            return false;
        }

        var nearH = new Vector4(ndcX, ndcY, 0f, 1f);
        var farH = new Vector4(ndcX, ndcY, 1f, 1f);

        Vector4 w0 = Vector4.Transform(nearH, invVp);
        Vector4 w1 = Vector4.Transform(farH, invVp);
        w0 /= w0.W;
        w1 /= w1.W;

        origin = new Vector3(w0.X, w0.Y, w0.Z);
        Vector3 d = new(w1.X - w0.X, w1.Y - w0.Y, w1.Z - w0.Z);
        if (d.LengthSquared() < 1e-10f)
        {
            return false;
        }

        dir = Vector3.Normalize(d);
        return true;
    }

    private static void SaveVolume(DenseRgbVoxelVolume volume, string path)
    {
        SvxvFileFormat.Save(path, volume);
    }

    private static DenseRgbVoxelVolume TryLoadVolume(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new DenseRgbVoxelVolume(VoxelSize, VoxelSize, VoxelSize);
            }

            return SvxvFileFormat.Load(path);
        }
        catch
        {
            return new DenseRgbVoxelVolume(VoxelSize, VoxelSize, VoxelSize);
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

