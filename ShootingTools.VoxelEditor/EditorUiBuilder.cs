using System.Numerics;
using ShootingEngine.Graphics;
using Vortice.Mathematics;

namespace ShootingTools.VoxelEditor;

public enum VoxelEditorToolKind
{
    Draw,
    Erase,
}

/// <summary>
/// Builds clip-space triangles for a simple left palette + right tool strip (MagicaVoxel-inspired layout).
/// </summary>
public static class EditorUiBuilder
{
    public const int PaletteWidth = 220;
    public const int ToolPanelWidth = 140;
    public const int SwatchSize = 22;
    public const int SwatchGap = 4;
    public const int PaletteCols = 8;
    public const int PaletteRows = 8;

    public static Color4[] BuildDefaultPalette()
    {
        var colors = new Color4[PaletteCols * PaletteRows];
        int idx = 0;
        for (int r = 0; r < PaletteRows; r++)
        {
            for (int c = 0; c < PaletteCols; c++)
            {
                float hue = (c + r * PaletteCols) / (float)(PaletteCols * PaletteRows);
                Color4 rgb = ColorFromHsv(hue * 360f, 0.55f + (r / (float)PaletteRows) * 0.35f, 0.35f + (c / (float)PaletteCols) * 0.55f);
                colors[idx++] = rgb;
            }
        }

        return colors;
    }

    public static void BuildUi(
        int windowW,
        int windowH,
        int mouseX,
        int mouseY,
        ReadOnlySpan<Color4> palette,
        int activePaletteIndex,
        int hoverPaletteIndex,
        VoxelEditorToolKind tool,
        VoxelEditorToolKind hoverTool,
        in Color4 activeCustomColor,
        List<UiVertex> outVerts)
    {
        outVerts.Clear();

        // Left panel background
        PushRect(outVerts, windowW, windowH, 0, 0, PaletteWidth, windowH, new Color4(0.06f, 0.06f, 0.08f, 0.92f));

        // Right panel background
        PushRect(outVerts, windowW, windowH, windowW - ToolPanelWidth, 0, ToolPanelWidth, windowH, new Color4(0.06f, 0.06f, 0.08f, 0.92f));

        int ox = 10;
        int oy = 12;

        for (int i = 0; i < palette.Length; i++)
        {
            int col = i % PaletteCols;
            int row = i / PaletteCols;
            int x = ox + col * (SwatchSize + SwatchGap);
            int y = oy + row * (SwatchSize + SwatchGap);
            bool hot = i == hoverPaletteIndex;
            bool sel = i == activePaletteIndex;
            Color4 border = hot ? new Color4(0.35f, 0.85f, 1f, 1f) : sel ? new Color4(1f, 0.85f, 0.25f, 1f) : new Color4(0.15f, 0.15f, 0.18f, 1f);
            PushBorder(outVerts, windowW, windowH, x - 2, y - 2, SwatchSize + 4, SwatchSize + 4, border, 2);
            PushRect(outVerts, windowW, windowH, x, y, SwatchSize, SwatchSize, palette[i]);
        }

        // Current custom color preview (from keyboard tweaks)
        int py = oy + PaletteRows * (SwatchSize + SwatchGap) + 14;
        PushBorder(outVerts, windowW, windowH, ox - 2, py - 2, SwatchSize * 2 + SwatchGap + 4, SwatchSize + 4, new Color4(0.35f, 0.85f, 1f, 1f), 2);
        PushRect(outVerts, windowW, windowH, ox, py, SwatchSize * 2 + SwatchGap, SwatchSize, activeCustomColor);

        // Tools on the right
        int tx = windowW - ToolPanelWidth + 16;
        int ty = 24;
        int bw = ToolPanelWidth - 32;
        int bh = 44;

        bool drawHot = hoverTool == VoxelEditorToolKind.Draw;
        bool eraseHot = hoverTool == VoxelEditorToolKind.Erase;
        PushToolButton(outVerts, windowW, windowH, tx, ty, bw, bh, tool == VoxelEditorToolKind.Draw, drawHot);
        PushToolButton(outVerts, windowW, windowH, tx, ty + bh + 12, bw, bh, tool == VoxelEditorToolKind.Erase, eraseHot);

        // Tool labels (tiny built-in pixel font).
        var textColor = new Color4(0.92f, 0.94f, 0.98f, 1f);
        PushTextCentered(outVerts, windowW, windowH, tx, ty, bw, bh, "DRAW", textColor, 2);
        PushTextCentered(outVerts, windowW, windowH, tx, ty + bh + 12, bw, bh, "ERASE", textColor, 2);
    }

    public static bool HitPalette(int mx, int my, out int index)
    {
        index = -1;
        if (mx < 0 || mx >= PaletteWidth)
        {
            return false;
        }

        int ox = 10;
        int oy = 12;
        int localX = mx - ox;
        int localY = my - oy;
        if (localX < 0 || localY < 0)
        {
            return false;
        }

        int cell = SwatchSize + SwatchGap;
        int col = localX / cell;
        int row = localY / cell;
        if (col < 0 || col >= PaletteCols || row < 0 || row >= PaletteRows)
        {
            return false;
        }

        int sx = localX - col * cell;
        int sy = localY - row * cell;
        if (sx >= SwatchSize || sy >= SwatchSize)
        {
            return false;
        }

        index = row * PaletteCols + col;
        return true;
    }

    public static bool HitDrawTool(int windowW, int mx, int my)
    {
        int tx = windowW - ToolPanelWidth + 16;
        int ty = 24;
        int bw = ToolPanelWidth - 32;
        int bh = 44;
        return mx >= tx && mx < tx + bw && my >= ty && my < ty + bh;
    }

    public static bool HitEraseTool(int windowW, int mx, int my)
    {
        int tx = windowW - ToolPanelWidth + 16;
        int ty = 24 + 44 + 12;
        int bw = ToolPanelWidth - 32;
        int bh = 44;
        return mx >= tx && mx < tx + bw && my >= ty && my < ty + bh;
    }

    public static bool IsIn3DViewport(int windowW, int mx, int my)
    {
        return mx >= PaletteWidth && mx < windowW - ToolPanelWidth;
    }

    private static void PushToolButton(List<UiVertex> vb, int w, int h, int x, int y, int rw, int rh, bool selected, bool hot)
    {
        Color4 bg = selected ? new Color4(0.18f, 0.32f, 0.22f, 0.95f) : new Color4(0.12f, 0.12f, 0.14f, 0.95f);
        if (hot && !selected)
        {
            bg = new Color4(0.16f, 0.22f, 0.30f, 0.95f);
        }

        Color4 border = selected ? new Color4(0.55f, 0.95f, 0.45f, 1f) : hot ? new Color4(0.35f, 0.85f, 1f, 1f) : new Color4(0.22f, 0.22f, 0.26f, 1f);
        PushBorder(vb, w, h, x - 2, y - 2, rw + 4, rh + 4, border, 2);
        PushRect(vb, w, h, x, y, rw, rh, bg);
    }

    private static void PushBorder(List<UiVertex> vb, int w, int h, int x, int y, int rw, int rh, in Color4 color, int thickness)
    {
        PushRect(vb, w, h, x, y, rw, thickness, color);
        PushRect(vb, w, h, x, y + rh - thickness, rw, thickness, color);
        PushRect(vb, w, h, x, y, thickness, rh, color);
        PushRect(vb, w, h, x + rw - thickness, y, thickness, rh, color);
    }

    private static void PushRect(List<UiVertex> vb, int w, int h, int px, int py, int rw, int rh, in Color4 color)
    {
        Vector4 a = PixelToClip(px, py, w, h);
        Vector4 b = PixelToClip(px + rw, py, w, h);
        Vector4 c = PixelToClip(px + rw, py + rh, w, h);
        Vector4 d = PixelToClip(px, py + rh, w, h);

        vb.Add(new UiVertex(a, color));
        vb.Add(new UiVertex(b, color));
        vb.Add(new UiVertex(c, color));

        vb.Add(new UiVertex(a, color));
        vb.Add(new UiVertex(c, color));
        vb.Add(new UiVertex(d, color));
    }

    private static void PushTextCentered(List<UiVertex> vb, int w, int h, int x, int y, int rw, int rh, ReadOnlySpan<char> text, in Color4 color, int scale)
    {
        int tw = MeasureTextWidth(text, scale);
        int th = 7 * scale;
        int px = x + (rw - tw) / 2;
        int py = y + (rh - th) / 2;
        PushText(vb, w, h, px, py, text, color, scale);
    }

    private static int MeasureTextWidth(ReadOnlySpan<char> text, int scale)
    {
        if (text.IsEmpty)
        {
            return 0;
        }

        // 5x7 glyphs + 1px spacing.
        return text.Length * (6 * scale) - (1 * scale);
    }

    private static void PushText(List<UiVertex> vb, int w, int h, int x, int y, ReadOnlySpan<char> text, in Color4 color, int scale)
    {
        int cursorX = x;
        for (int i = 0; i < text.Length; i++)
        {
            if (TryGetGlyph5x7(text[i], out ulong bits))
            {
                // bits are row-major, 5 bits per row, 7 rows (LSB = x0,y0).
                for (int gy = 0; gy < 7; gy++)
                {
                    for (int gx = 0; gx < 5; gx++)
                    {
                        int bitIndex = gy * 5 + gx;
                        if (((bits >> bitIndex) & 1UL) != 0UL)
                        {
                            PushRect(vb, w, h, cursorX + gx * scale, y + gy * scale, scale, scale, color);
                        }
                    }
                }
            }

            cursorX += 6 * scale;
        }
    }

    private static bool TryGetGlyph5x7(char c, out ulong bits)
    {
        // Minimal glyph set for DRAW / ERASE (uppercase) + space.
        c = char.ToUpperInvariant(c);
        bits = c switch
        {
            ' ' => 0UL,

            // D
            'D' => Glyph(
                "11110",
                "10001",
                "10001",
                "10001",
                "10001",
                "10001",
                "11110"),

            // R
            'R' => Glyph(
                "11110",
                "10001",
                "10001",
                "11110",
                "10100",
                "10010",
                "10001"),

            // A
            'A' => Glyph(
                "01110",
                "10001",
                "10001",
                "11111",
                "10001",
                "10001",
                "10001"),

            // W
            'W' => Glyph(
                "10001",
                "10001",
                "10001",
                "10101",
                "10101",
                "11011",
                "10001"),

            // E
            'E' => Glyph(
                "11111",
                "10000",
                "10000",
                "11110",
                "10000",
                "10000",
                "11111"),

            // S
            'S' => Glyph(
                "01111",
                "10000",
                "10000",
                "01110",
                "00001",
                "00001",
                "11110"),

            _ => 0UL,
        };

        return bits != 0UL || c == ' ';

        static ulong Glyph(string r0, string r1, string r2, string r3, string r4, string r5, string r6)
        {
            Span<string> rows = [r0, r1, r2, r3, r4, r5, r6];
            ulong b = 0UL;
            for (int y = 0; y < 7; y++)
            {
                string row = rows[y];
                for (int x = 0; x < 5; x++)
                {
                    if (row[x] == '1')
                    {
                        b |= 1UL << (y * 5 + x);
                    }
                }
            }
            return b;
        }
    }

    private static Vector4 PixelToClip(int px, int py, int w, int h)
    {
        float ndcX = (px / (float)w) * 2f - 1f;
        float ndcY = 1f - (py / (float)h) * 2f;
        return new Vector4(ndcX, ndcY, 0f, 1f);
    }

    private static Color4 ColorFromHsv(float hDeg, float s, float v)
    {
        float hh = (hDeg % 360f + 360f) % 360f / 60f;
        int sector = (int)MathF.Floor(hh);
        float frac = hh - sector;
        float p = v * (1f - s);
        float q = v * (1f - s * frac);
        float t = v * (1f - s * (1f - frac));
        return sector switch
        {
            0 => new Color4(v, t, p, 1f),
            1 => new Color4(q, v, p, 1f),
            2 => new Color4(p, v, t, 1f),
            3 => new Color4(p, q, v, 1f),
            4 => new Color4(t, p, v, 1f),
            _ => new Color4(v, p, q, 1f),
        };
    }
}

