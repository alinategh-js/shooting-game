using System.Numerics;

namespace ShootingGame;

/// <summary>Axis-aligned box used for static level collision.</summary>
public readonly struct Aabb
{
    public readonly Vector3 Min;
    public readonly Vector3 Max;

    public Aabb(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public static Aabb FromCenterHalfExtents(Vector3 center, Vector3 halfExtents)
    {
        return new Aabb(center - halfExtents, center + halfExtents);
    }

    public Vector3 Center => (Min + Max) * 0.5f;

    public static bool Intersects(in Aabb a, in Aabb b)
    {
        return a.Min.X < b.Max.X && a.Max.X > b.Min.X
            && a.Min.Y < b.Max.Y && a.Max.Y > b.Min.Y
            && a.Min.Z < b.Max.Z && a.Max.Z > b.Min.Z;
    }
}
