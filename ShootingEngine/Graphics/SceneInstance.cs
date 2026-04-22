using System.Numerics;
using Vortice.Mathematics;

namespace ShootingEngine.Graphics;

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

