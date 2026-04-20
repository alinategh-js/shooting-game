using System.Numerics;

namespace ShootingGame;

/// <summary>
/// Axis-aligned player (feet at box min-Y) vs static world AABBs: separation + grounded detection.
/// </summary>
public static class PlayerCollision
{
    public const float Gravity = 22f;
    public const float TerminalVelocity = 48f;
    public const float JumpSpeed = 7.2f;
    public const float GroundProbe = 0.07f;

    public static Aabb PlayerBounds(in Vector3 feet, in PlayerColliderDims dims)
    {
        float y0 = feet.Y;
        float y1 = feet.Y + dims.Height;
        return new Aabb(
            new Vector3(feet.X - dims.HalfWidth, y0, feet.Z - dims.HalfDepth),
            new Vector3(feet.X + dims.HalfWidth, y1, feet.Z + dims.HalfDepth));
    }

    public static void IntegrateAndResolve(
        ref Vector3 feet,
        ref Vector3 velocity,
        in PlayerColliderDims dims,
        ReadOnlySpan<Aabb> world,
        float deltaSeconds,
        bool jumpPressed,
        ref bool isGrounded)
    {
        bool wasGrounded = isGrounded;

        velocity.Y -= Gravity * deltaSeconds;
        if (velocity.Y < -TerminalVelocity)
        {
            velocity.Y = -TerminalVelocity;
        }

        if (jumpPressed && wasGrounded && velocity.Y <= 0.12f)
        {
            velocity.Y = JumpSpeed;
        }

        feet += velocity * deltaSeconds;

        for (int iter = 0; iter < 6; iter++)
        {
            Aabb player = PlayerBounds(feet, dims);
            bool any = false;
            for (int i = 0; i < world.Length; i++)
            {
                if (TrySeparate(ref feet, ref velocity, in dims, in player, world[i]))
                {
                    any = true;
                    player = PlayerBounds(feet, dims);
                }
            }

            if (!any)
            {
                break;
            }
        }

        isGrounded = IsGrounded(feet, in dims, world, velocity.Y);
        if (isGrounded && velocity.Y < 0f)
        {
            velocity.Y = 0f;
        }
    }

    private static bool IsGrounded(in Vector3 feet, in PlayerColliderDims dims, ReadOnlySpan<Aabb> world, float velY)
    {
        if (velY > 0.25f)
        {
            return false;
        }

        const float eps = 0.06f;
        var probe = new Aabb(
            new Vector3(feet.X - dims.HalfWidth, feet.Y - eps, feet.Z - dims.HalfDepth),
            new Vector3(feet.X + dims.HalfWidth, feet.Y + 0.1f, feet.Z + dims.HalfDepth));

        for (int i = 0; i < world.Length; i++)
        {
            if (!Aabb.Intersects(probe, world[i]))
            {
                continue;
            }

            float dy = feet.Y - world[i].Max.Y;
            if (dy >= -0.02f && dy <= 0.1f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySeparate(
        ref Vector3 feet,
        ref Vector3 velocity,
        in PlayerColliderDims dims,
        in Aabb player,
        in Aabb wall)
    {
        if (!Aabb.Intersects(player, wall))
        {
            return false;
        }

        float overlapX = Math.Min(player.Max.X, wall.Max.X) - Math.Max(player.Min.X, wall.Min.X);
        float overlapY = Math.Min(player.Max.Y, wall.Max.Y) - Math.Max(player.Min.Y, wall.Min.Y);
        float overlapZ = Math.Min(player.Max.Z, wall.Max.Z) - Math.Max(player.Min.Z, wall.Min.Z);
        if (overlapX <= 0f || overlapY <= 0f || overlapZ <= 0f)
        {
            return false;
        }

        Vector3 pCenter = player.Center;
        Vector3 wCenter = wall.Center;

        if (overlapX < overlapY && overlapX < overlapZ)
        {
            float dir = pCenter.X < wCenter.X ? -1f : 1f;
            feet.X += overlapX * dir;
            velocity.X = 0f;
        }
        else if (overlapY < overlapZ)
        {
            float dir = pCenter.Y < wCenter.Y ? -1f : 1f;
            feet.Y += overlapY * dir;
            if (dir > 0f && velocity.Y < 0f)
            {
                velocity.Y = 0f;
            }
            else if (dir < 0f && velocity.Y > 0f)
            {
                velocity.Y = 0f;
            }
        }
        else
        {
            float dir = pCenter.Z < wCenter.Z ? -1f : 1f;
            feet.Z += overlapZ * dir;
            velocity.Z = 0f;
        }

        return true;
    }
}

public readonly struct PlayerColliderDims
{
    public readonly float HalfWidth;
    public readonly float HalfDepth;
    public readonly float Height;

    public PlayerColliderDims(float halfWidth, float halfDepth, float height)
    {
        HalfWidth = halfWidth;
        HalfDepth = halfDepth;
        Height = height;
    }

    public static PlayerColliderDims Standing => new(0.28f, 0.28f, 1.78f);
    public static PlayerColliderDims Crouching => new(0.28f, 0.28f, 1.05f);
}
