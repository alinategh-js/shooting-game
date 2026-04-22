using System.Numerics;
using Hexa.NET.SDL2;

namespace ShootingEngine.Cameras;

/// <summary>
/// Editor-friendly free-fly camera (no collision): WASD to move, mouse to look.
/// </summary>
public struct FlyCamera
{
    public const float MouseSensitivity = 0.0022f;

    public Vector3 Position;
    public float Yaw;
    public float Pitch;

    public float BaseSpeed;
    public float FastMultiplier;
    public float SlowMultiplier;

    public static FlyCamera CreateDefault(in Vector3 position)
    {
        return new FlyCamera
        {
            Position = position,
            Yaw = 0f,
            Pitch = 0f,
            BaseSpeed = 3.5f,
            FastMultiplier = 3.2f,
            SlowMultiplier = 0.35f,
        };
    }

    public void ApplyMouseLook(int deltaX, int deltaY)
    {
        Yaw -= deltaX * MouseSensitivity;
        Pitch -= deltaY * MouseSensitivity;
        float limit = MathF.PI / 2f - 0.01f;
        Pitch = Math.Clamp(Pitch, -limit, limit);
    }

    public readonly Matrix4x4 GetViewMatrix()
    {
        GetLookDirection(out Vector3 forward);
        return Matrix4x4.CreateLookAt(Position, Position + forward, Vector3.UnitY);
    }

    public readonly void GetLookDirection(out Vector3 forward)
    {
        float cp = MathF.Cos(Pitch);
        float sp = MathF.Sin(Pitch);
        float cy = MathF.Cos(Yaw);
        float sy = MathF.Sin(Yaw);
        forward = Vector3.Normalize(new Vector3(sy * cp, sp, cy * cp));
    }

    public readonly void GetBasis(out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        GetLookDirection(out forward);
        right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        up = Vector3.Normalize(Vector3.Cross(right, forward));
    }

    public unsafe void UpdateMovement(float dt, byte* keys, int keyCount)
    {
        if (dt <= 0f)
        {
            return;
        }

        bool Down(SDLScancode sc)
        {
            int i = (int)sc;
            return i >= 0 && i < keyCount && keys[i] != 0;
        }

        float speed = BaseSpeed;
        if (Down(SDLScancode.Lshift) || Down(SDLScancode.Rshift))
        {
            speed *= FastMultiplier;
        }
        if (Down(SDLScancode.Lctrl) || Down(SDLScancode.Rctrl))
        {
            speed *= SlowMultiplier;
        }

        GetBasis(out Vector3 forward, out Vector3 right, out _);
        forward = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));
        right = Vector3.Normalize(new Vector3(right.X, 0f, right.Z));

        Vector3 wish = Vector3.Zero;
        if (Down(SDLScancode.W)) wish += forward;
        if (Down(SDLScancode.S)) wish -= forward;
        if (Down(SDLScancode.D)) wish += right;
        if (Down(SDLScancode.A)) wish -= right;
        if (Down(SDLScancode.E)) wish += Vector3.UnitY;
        if (Down(SDLScancode.Q)) wish -= Vector3.UnitY;

        if (wish.LengthSquared() > 1e-8f)
        {
            wish = Vector3.Normalize(wish);
            Position += wish * (speed * dt);
        }
    }
}

