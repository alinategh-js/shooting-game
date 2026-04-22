using System.Numerics;
using ShootingEngine.Graphics;
using Vortice.Mathematics;

namespace ShootingGame;

/// <summary>
/// Procedural room: floor, ceiling, walls, and accent cubes — all axis-aligned boxes, no external models.
/// </summary>
public static class SceneLevel
{
    public const float RoomHalfX = 18.5f;
    public const float RoomHalfZ = 16.5f;
    public const float WallThickness = 0.55f;
    public const float WallHalfY = 2.75f;

    /// <summary>Static colliders matching visible geometry (except purely decorative trims that sit on surfaces).</summary>
    public static readonly Aabb[] Colliders = BuildColliders();

    /// <summary>Instances for the renderer: world matrix + tint.</summary>
    public static void AppendDrawInstances(float simTimeSeconds, List<SceneInstance> list)
    {
        float breathe = 0.92f + 0.08f * MathF.Sin(simTimeSeconds * 1.7f);
        float pulse = 0.88f + 0.12f * MathF.Sin(simTimeSeconds * 2.3f + 0.7f);
        float flicker = 0.8f + 0.2f * MathF.Sin(simTimeSeconds * 9.0f + 1.1f);

        // Floor — warmer stone with a slightly brighter path band.
        list.Add(Box(new Vector3(0f, -0.26f, 0f), new Vector3(RoomHalfX + WallThickness, 0.26f, RoomHalfZ + WallThickness), new Color4(0.26f, 0.24f, 0.22f, 1f)));
        list.Add(Box(new Vector3(0f, 0.02f, 0f), new Vector3(RoomHalfX - 1.4f, 0.03f, 0.55f), new Color4(0.34f, 0.36f, 0.33f, 1f)));

        // Ceiling — cool panels (darker) so the walls read better.
        list.Add(Box(new Vector3(0f, WallHalfY * 2f + 0.28f, 0f), new Vector3(RoomHalfX + WallThickness, 0.20f, RoomHalfZ + WallThickness), new Color4(0.10f, 0.13f, 0.20f, 1f)));

        float outerX = RoomHalfX + WallThickness * 0.5f;
        float outerZ = RoomHalfZ + WallThickness * 0.5f;

        // Walls — more saturated so the lighting has something to work with.
        Color4 wallNorth = new(0.14f, 0.36f, 0.44f, 1f);
        Color4 wallSouth = new(0.18f, 0.42f, 0.36f, 1f);
        Color4 wallWest = new(0.16f, 0.30f, 0.50f, 1f);
        Color4 wallEast = new(0.22f, 0.38f, 0.42f, 1f);

        list.Add(Box(new Vector3(0f, WallHalfY, -(RoomHalfZ + WallThickness * 0.5f)), new Vector3(outerX, WallHalfY, WallThickness * 0.5f), wallNorth));
        list.Add(Box(new Vector3(0f, WallHalfY, RoomHalfZ + WallThickness * 0.5f), new Vector3(outerX, WallHalfY, WallThickness * 0.5f), wallSouth));
        list.Add(Box(new Vector3(-(RoomHalfX + WallThickness * 0.5f), WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ), wallWest));
        list.Add(Box(new Vector3(RoomHalfX + WallThickness * 0.5f, WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ), wallEast));

        // Baseboards (wood-like trim)
        float trimH = 0.16f;
        Color4 trim = new(0.58f, 0.34f, 0.16f, 1f);
        list.Add(Box(new Vector3(0f, trimH * 0.5f, -(RoomHalfZ - 0.05f)), new Vector3(RoomHalfX - 0.2f, trimH, 0.12f), Mul(trim, breathe)));
        list.Add(Box(new Vector3(0f, trimH * 0.5f, RoomHalfZ - 0.05f), new Vector3(RoomHalfX - 0.2f, trimH, 0.12f), Mul(trim, breathe)));
        list.Add(Box(new Vector3(-(RoomHalfX - 0.05f), trimH * 0.5f, 0f), new Vector3(0.12f, trimH, RoomHalfZ - 0.2f), Mul(trim, pulse)));
        list.Add(Box(new Vector3(RoomHalfX - 0.05f, trimH * 0.5f, 0f), new Vector3(0.12f, trimH, RoomHalfZ - 0.2f), Mul(trim, pulse)));

        // Corner columns (stone-like, chunky)
        Color4 stoneA = new(0.36f, 0.36f, 0.38f, 1f);
        Color4 stoneB = new(0.30f, 0.32f, 0.35f, 1f);
        float colR = 0.55f;
        float colY = WallHalfY;
        Vector3 colHalf = new(colR, colY, colR);
        list.Add(Box(new Vector3(-RoomHalfX + 1.6f, colY, -RoomHalfZ + 1.6f), colHalf, Mul(stoneA, breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 1.6f, colY, -RoomHalfZ + 1.6f), colHalf, Mul(stoneB, pulse)));
        list.Add(Box(new Vector3(-RoomHalfX + 1.6f, colY, RoomHalfZ - 1.6f), colHalf, Mul(stoneB, breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 1.6f, colY, RoomHalfZ - 1.6f), colHalf, Mul(stoneA, pulse)));

        // Central low table / platform (wood base + green felt top)
        list.Add(Box(new Vector3(0f, 0.35f, 3.2f), new Vector3(2.8f, 0.35f, 1.45f), new Color4(0.42f, 0.26f, 0.16f, 1f)));
        list.Add(Box(new Vector3(0f, 0.80f, 3.2f), new Vector3(2.45f, 0.09f, 1.15f), new Color4(0.18f, 0.48f, 0.26f, 1f)));

        // Scatter "crates" for depth
        list.Add(Box(new Vector3(-5.8f, 0.45f, -7.2f), new Vector3(0.55f, 0.45f, 0.55f), new Color4(0.66f, 0.34f, 0.18f, 1f)));
        list.Add(Box(new Vector3(-5.1f, 0.45f, -7.2f), new Vector3(0.55f, 0.45f, 0.55f), new Color4(0.58f, 0.30f, 0.16f, 1f)));
        list.Add(Box(new Vector3(7.5f, 0.55f, -6.2f), new Vector3(0.65f, 0.55f, 0.65f), new Color4(0.24f, 0.52f, 0.78f, 1f)));

        // A couple of poly \"trees\" (stacked cubes) in the far corners to sell the low-poly vibe.
        AddPolyTree(list, new Vector3(-RoomHalfX + 4.6f, 0f, RoomHalfZ - 4.2f), simTimeSeconds, new Color4(0.60f, 0.40f, 0.20f, 1f), new Color4(0.34f, 0.72f, 0.26f, 1f));
        AddPolyTree(list, new Vector3(RoomHalfX - 4.6f, 0f, RoomHalfZ - 4.8f), simTimeSeconds + 2.2f, new Color4(0.56f, 0.36f, 0.18f, 1f), new Color4(0.28f, 0.66f, 0.30f, 1f));

        // Wall sconce lights (emissive-ish bright cubes)
        Color4 sconce = new(1f, 0.92f, 0.62f, 1f);
        list.Add(Box(new Vector3(-RoomHalfX + 0.75f, 1.85f, -2.2f), new Vector3(0.14f, 0.22f, 0.45f), Mul(sconce, 0.75f + 0.25f * breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 0.75f, 1.85f, -2.2f), new Vector3(0.14f, 0.22f, 0.45f), Mul(sconce, 0.75f + 0.25f * pulse)));
        list.Add(Box(new Vector3(-2.5f, 2.05f, -RoomHalfZ + 0.75f), new Vector3(0.45f, 0.10f, 0.14f), Mul(sconce, 0.6f + 0.4f * flicker)));
        list.Add(Box(new Vector3(2.5f, 2.05f, -RoomHalfZ + 0.75f), new Vector3(0.45f, 0.10f, 0.14f), Mul(sconce, 0.6f + 0.4f * (1f - flicker))));
    }

    private static Color4 Mul(in Color4 c, float m) => new(c.R * m, c.G * m, c.B * m, c.A);

    private static SceneInstance Box(Vector3 center, Vector3 halfExtents, Color4 tint)
    {
        Matrix4x4 scale = Matrix4x4.CreateScale(halfExtents.X * 2f, halfExtents.Y * 2f, halfExtents.Z * 2f);
        Matrix4x4 world = Matrix4x4.Multiply(scale, Matrix4x4.CreateTranslation(center));
        return new SceneInstance(world, tint);
    }

    private static void AddPolyTree(List<SceneInstance> list, Vector3 basePos, float t, Color4 trunk, Color4 leaf)
    {
        // Trunk (tapered stack)
        float sway = MathF.Sin(t * 1.2f) * 0.06f;
        list.Add(Box(basePos + new Vector3(0f, 0.35f, 0f), new Vector3(0.16f, 0.35f, 0.16f), trunk));
        list.Add(Box(basePos + new Vector3(sway * 0.25f, 0.86f, 0f), new Vector3(0.13f, 0.30f, 0.13f), trunk));
        list.Add(Box(basePos + new Vector3(sway * 0.55f, 1.28f, 0f), new Vector3(0.10f, 0.22f, 0.10f), trunk));

        // Canopy (cluster)
        Vector3 c = basePos + new Vector3(sway, 1.75f, 0f);
        Color4 l0 = Mul(leaf, 0.95f + 0.05f * MathF.Sin(t * 2.0f));
        Color4 l1 = Mul(leaf, 0.85f + 0.10f * MathF.Sin(t * 1.6f + 1.2f));
        list.Add(Box(c + new Vector3(0f, 0.10f, 0f), new Vector3(0.55f, 0.42f, 0.55f), l0));
        list.Add(Box(c + new Vector3(0.55f, -0.05f, 0.15f), new Vector3(0.35f, 0.30f, 0.35f), l1));
        list.Add(Box(c + new Vector3(-0.55f, -0.02f, -0.10f), new Vector3(0.32f, 0.28f, 0.32f), l1));
        list.Add(Box(c + new Vector3(0.10f, 0.45f, -0.40f), new Vector3(0.26f, 0.22f, 0.26f), l0));
    }

    private static Aabb[] BuildColliders()
    {
        // Keep colliders aligned with major solids (floor, ceiling, walls, columns, table, crates).
        var list = new List<Aabb>(24);
        void AddBox(Vector3 c, Vector3 half) => list.Add(Aabb.FromCenterHalfExtents(c, half));

        AddBox(new Vector3(0f, -0.26f, 0f), new Vector3(RoomHalfX + WallThickness, 0.26f, RoomHalfZ + WallThickness));
        AddBox(new Vector3(0f, WallHalfY * 2f + 0.28f, 0f), new Vector3(RoomHalfX + WallThickness, 0.20f, RoomHalfZ + WallThickness));

        float outerX = RoomHalfX + WallThickness * 0.5f;
        float outerZ = RoomHalfZ + WallThickness * 0.5f;
        AddBox(new Vector3(0f, WallHalfY, -(RoomHalfZ + WallThickness * 0.5f)), new Vector3(outerX, WallHalfY, WallThickness * 0.5f));
        AddBox(new Vector3(0f, WallHalfY, RoomHalfZ + WallThickness * 0.5f), new Vector3(outerX, WallHalfY, WallThickness * 0.5f));
        AddBox(new Vector3(-(RoomHalfX + WallThickness * 0.5f), WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ));
        AddBox(new Vector3(RoomHalfX + WallThickness * 0.5f, WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ));

        float colR = 0.55f;
        float colY = WallHalfY;
        Vector3 colHalf = new(colR, colY, colR);
        AddBox(new Vector3(-RoomHalfX + 1.6f, colY, -RoomHalfZ + 1.6f), colHalf);
        AddBox(new Vector3(RoomHalfX - 1.6f, colY, -RoomHalfZ + 1.6f), colHalf);
        AddBox(new Vector3(-RoomHalfX + 1.6f, colY, RoomHalfZ - 1.6f), colHalf);
        AddBox(new Vector3(RoomHalfX - 1.6f, colY, RoomHalfZ - 1.6f), colHalf);

        AddBox(new Vector3(0f, 0.35f, 3.2f), new Vector3(2.8f, 0.35f, 1.45f));
        AddBox(new Vector3(0f, 0.80f, 3.2f), new Vector3(2.45f, 0.09f, 1.15f));

        AddBox(new Vector3(-5.8f, 0.45f, -7.2f), new Vector3(0.55f, 0.45f, 0.55f));
        AddBox(new Vector3(-5.1f, 0.45f, -7.2f), new Vector3(0.55f, 0.45f, 0.55f));
        AddBox(new Vector3(7.5f, 0.55f, -6.2f), new Vector3(0.65f, 0.55f, 0.65f));

        return list.ToArray();
    }
}

// SceneInstance lives in ShootingEngine.Graphics
