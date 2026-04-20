using System.Numerics;
using Vortice.Mathematics;

namespace ShootingGame;

/// <summary>
/// Procedural room: floor, ceiling, walls, and accent cubes — all axis-aligned boxes, no external models.
/// </summary>
public static class SceneLevel
{
    public const float RoomHalfX = 11f;
    public const float RoomHalfZ = 10f;
    public const float WallThickness = 0.38f;
    public const float WallHalfY = 2.05f;

    /// <summary>Static colliders matching visible geometry (except purely decorative trims that sit on surfaces).</summary>
    public static readonly Aabb[] Colliders = BuildColliders();

    /// <summary>Instances for the renderer: world matrix + tint.</summary>
    public static void AppendDrawInstances(float simTimeSeconds, List<SceneInstance> list)
    {
        float breathe = 0.92f + 0.08f * MathF.Sin(simTimeSeconds * 1.7f);
        float pulse = 0.88f + 0.12f * MathF.Sin(simTimeSeconds * 2.3f + 0.7f);

        // Floor — warm slate concrete
        list.Add(Box(new Vector3(0f, -0.22f, 0f), new Vector3(RoomHalfX + WallThickness, 0.22f, RoomHalfZ + WallThickness), new Color4(0.22f, 0.24f, 0.28f, 1f)));
        // Inlaid floor band
        list.Add(Box(new Vector3(0f, 0.02f, 0f), new Vector3(RoomHalfX - 0.6f, 0.03f, 0.35f), new Color4(0.36f, 0.52f, 0.62f, 1f)));

        // Ceiling — deep blue panels
        list.Add(Box(new Vector3(0f, WallHalfY * 2f + 0.22f, 0f), new Vector3(RoomHalfX + WallThickness, 0.18f, RoomHalfZ + WallThickness), new Color4(0.12f, 0.16f, 0.26f, 1f)));

        float outerX = RoomHalfX + WallThickness * 0.5f;
        float outerZ = RoomHalfZ + WallThickness * 0.5f;

        // Walls — layered tones (outer shell slightly darker)
        Color4 wallNorth = new(0.18f, 0.32f, 0.38f, 1f);
        Color4 wallSouth = new(0.20f, 0.36f, 0.34f, 1f);
        Color4 wallWest = new(0.16f, 0.28f, 0.40f, 1f);
        Color4 wallEast = new(0.22f, 0.34f, 0.36f, 1f);

        list.Add(Box(new Vector3(0f, WallHalfY, -(RoomHalfZ + WallThickness * 0.5f)), new Vector3(outerX, WallHalfY, WallThickness * 0.5f), wallNorth));
        list.Add(Box(new Vector3(0f, WallHalfY, RoomHalfZ + WallThickness * 0.5f), new Vector3(outerX, WallHalfY, WallThickness * 0.5f), wallSouth));
        list.Add(Box(new Vector3(-(RoomHalfX + WallThickness * 0.5f), WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ), wallWest));
        list.Add(Box(new Vector3(RoomHalfX + WallThickness * 0.5f, WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ), wallEast));

        // Baseboards (warm trim)
        float trimH = 0.14f;
        Color4 trim = new(0.62f, 0.42f, 0.22f, 1f);
        list.Add(Box(new Vector3(0f, trimH * 0.5f, -(RoomHalfZ - 0.05f)), new Vector3(RoomHalfX - 0.2f, trimH, 0.12f), Mul(trim, breathe)));
        list.Add(Box(new Vector3(0f, trimH * 0.5f, RoomHalfZ - 0.05f), new Vector3(RoomHalfX - 0.2f, trimH, 0.12f), Mul(trim, breathe)));
        list.Add(Box(new Vector3(-(RoomHalfX - 0.05f), trimH * 0.5f, 0f), new Vector3(0.12f, trimH, RoomHalfZ - 0.2f), Mul(trim, pulse)));
        list.Add(Box(new Vector3(RoomHalfX - 0.05f, trimH * 0.5f, 0f), new Vector3(0.12f, trimH, RoomHalfZ - 0.2f), Mul(trim, pulse)));

        // Corner columns (vertical accents)
        Color4 colA = new(0.95f, 0.55f, 0.28f, 1f);
        Color4 colB = new(0.55f, 0.35f, 0.92f, 1f);
        float colR = 0.32f;
        float colY = WallHalfY;
        Vector3 colHalf = new(colR, colY, colR);
        list.Add(Box(new Vector3(-RoomHalfX + 1.1f, colY, -RoomHalfZ + 1.1f), colHalf, Mul(colA, breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 1.1f, colY, -RoomHalfZ + 1.1f), colHalf, Mul(colB, pulse)));
        list.Add(Box(new Vector3(-RoomHalfX + 1.1f, colY, RoomHalfZ - 1.1f), colHalf, Mul(colB, breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 1.1f, colY, RoomHalfZ - 1.1f), colHalf, Mul(colA, pulse)));

        // Central low table / platform
        list.Add(Box(new Vector3(0f, 0.35f, 2f), new Vector3(2.2f, 0.35f, 1.1f), new Color4(0.32f, 0.26f, 0.20f, 1f)));
        list.Add(Box(new Vector3(0f, 0.78f, 2f), new Vector3(1.9f, 0.08f, 0.9f), new Color4(0.55f, 0.62f, 0.38f, 1f)));

        // Scatter "crates" for depth
        list.Add(Box(new Vector3(-3.5f, 0.45f, -4f), new Vector3(0.55f, 0.45f, 0.55f), new Color4(0.72f, 0.38f, 0.22f, 1f)));
        list.Add(Box(new Vector3(-2.9f, 0.45f, -4f), new Vector3(0.55f, 0.45f, 0.55f), new Color4(0.62f, 0.32f, 0.20f, 1f)));
        list.Add(Box(new Vector3(4.2f, 0.55f, -3.2f), new Vector3(0.65f, 0.55f, 0.65f), new Color4(0.28f, 0.48f, 0.72f, 1f)));

        // Wall sconce lights (emissive-ish bright cubes)
        Color4 sconce = new(1f, 0.92f, 0.62f, 1f);
        list.Add(Box(new Vector3(-RoomHalfX + 0.55f, 1.55f, 0f), new Vector3(0.12f, 0.18f, 0.35f), Mul(sconce, 0.85f + 0.15f * breathe)));
        list.Add(Box(new Vector3(RoomHalfX - 0.55f, 1.55f, 0f), new Vector3(0.12f, 0.18f, 0.35f), Mul(sconce, 0.85f + 0.15f * pulse)));
    }

    private static Color4 Mul(in Color4 c, float m) => new(c.R * m, c.G * m, c.B * m, c.A);

    private static SceneInstance Box(Vector3 center, Vector3 halfExtents, Color4 tint)
    {
        Matrix4x4 scale = Matrix4x4.CreateScale(halfExtents.X * 2f, halfExtents.Y * 2f, halfExtents.Z * 2f);
        Matrix4x4 world = Matrix4x4.Multiply(scale, Matrix4x4.CreateTranslation(center));
        return new SceneInstance(world, tint);
    }

    private static Aabb[] BuildColliders()
    {
        // Keep colliders aligned with major solids (floor, ceiling, walls, columns, table, crates).
        var list = new List<Aabb>(24);
        void AddBox(Vector3 c, Vector3 half) => list.Add(Aabb.FromCenterHalfExtents(c, half));

        AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(RoomHalfX + WallThickness, 0.22f, RoomHalfZ + WallThickness));
        AddBox(new Vector3(0f, WallHalfY * 2f + 0.22f, 0f), new Vector3(RoomHalfX + WallThickness, 0.18f, RoomHalfZ + WallThickness));

        float outerX = RoomHalfX + WallThickness * 0.5f;
        float outerZ = RoomHalfZ + WallThickness * 0.5f;
        AddBox(new Vector3(0f, WallHalfY, -(RoomHalfZ + WallThickness * 0.5f)), new Vector3(outerX, WallHalfY, WallThickness * 0.5f));
        AddBox(new Vector3(0f, WallHalfY, RoomHalfZ + WallThickness * 0.5f), new Vector3(outerX, WallHalfY, WallThickness * 0.5f));
        AddBox(new Vector3(-(RoomHalfX + WallThickness * 0.5f), WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ));
        AddBox(new Vector3(RoomHalfX + WallThickness * 0.5f, WallHalfY, 0f), new Vector3(WallThickness * 0.5f, WallHalfY, outerZ));

        float colR = 0.32f;
        float colY = WallHalfY;
        Vector3 colHalf = new(colR, colY, colR);
        AddBox(new Vector3(-RoomHalfX + 1.1f, colY, -RoomHalfZ + 1.1f), colHalf);
        AddBox(new Vector3(RoomHalfX - 1.1f, colY, -RoomHalfZ + 1.1f), colHalf);
        AddBox(new Vector3(-RoomHalfX + 1.1f, colY, RoomHalfZ - 1.1f), colHalf);
        AddBox(new Vector3(RoomHalfX - 1.1f, colY, RoomHalfZ - 1.1f), colHalf);

        AddBox(new Vector3(0f, 0.35f, 2f), new Vector3(2.2f, 0.35f, 1.1f));
        AddBox(new Vector3(0f, 0.78f, 2f), new Vector3(1.9f, 0.08f, 0.9f));

        AddBox(new Vector3(-3.5f, 0.45f, -4f), new Vector3(0.55f, 0.45f, 0.55f));
        AddBox(new Vector3(-2.9f, 0.45f, -4f), new Vector3(0.55f, 0.45f, 0.55f));
        AddBox(new Vector3(4.2f, 0.55f, -3.2f), new Vector3(0.65f, 0.55f, 0.65f));

        return list.ToArray();
    }
}

public readonly struct SceneInstance
{
    public readonly Matrix4x4 World;
    public readonly Color4 Tint;

    public SceneInstance(Matrix4x4 world, Color4 tint)
    {
        World = world;
        Tint = tint;
    }
}
