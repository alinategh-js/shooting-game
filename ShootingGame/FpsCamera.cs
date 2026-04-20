using System.Numerics;
using Hexa.NET.SDL2;

namespace ShootingGame;

/// <summary>
/// Simple yaw/pitch FPS-style camera: mouse steers, WASD moves on XZ, Space/Ctrl fly vertically.
/// </summary>
public struct FpsCamera
{
    public const float MouseSensitivity = 0.0022f;
    public const float MoveSpeed = 5f;

    public Vector3 Position;
    public float Yaw;
    public float Pitch;

    public static FpsCamera CreateDefault()
    {
        // Match the old hard-coded look-at-origin from (0, 1.2, -3.5).
        var pos = new Vector3(0f, 1.2f, -3.5f);
        var toTarget = Vector3.Normalize(Vector3.Zero - pos);
        float yaw = MathF.Atan2(toTarget.X, toTarget.Z);
        float pitch = MathF.Asin(Math.Clamp(toTarget.Y, -1f, 1f));
        return new FpsCamera { Position = pos, Yaw = yaw, Pitch = pitch };
    }

    public void ApplyMouseLook(int deltaX, int deltaY)
    {
        // Negate horizontal delta so "mouse right" turns view right (matches typical FPS feel on Windows).
        Yaw -= deltaX * MouseSensitivity;
        Pitch -= deltaY * MouseSensitivity;
        float limit = MathF.PI / 2f - 0.01f;
        Pitch = Math.Clamp(Pitch, -limit, limit);
    }

    public readonly Matrix4x4 GetViewMatrix()
    {
        float cp = MathF.Cos(Pitch);
        float sp = MathF.Sin(Pitch);
        float cy = MathF.Cos(Yaw);
        float sy = MathF.Sin(Yaw);

        var forward = Vector3.Normalize(new Vector3(sy * cp, sp, cy * cp));
        var target = Position + forward;
        return Matrix4x4.CreateLookAt(Position, target, Vector3.UnitY);
    }

    public unsafe void UpdateMovement(float deltaSeconds, byte* keys, int keyCount)
    {
        bool Down(SDLScancode sc)
        {
            int i = (int)sc;
            return i >= 0 && i < keyCount && keys[i] != 0;
        }

        float sy = MathF.Sin(Yaw);
        float cy = MathF.Cos(Yaw);
        var forwardFlat = new Vector3(sy, 0f, cy);
        if (forwardFlat.LengthSquared() > 1e-8f)
        {
            forwardFlat = Vector3.Normalize(forwardFlat);
        }

        // Right = forward × worldUp (Y). Previous (cos, 0, -sin) was the opposite sign, swapping A/D.
        var rightFlat = Vector3.Normalize(Vector3.Cross(forwardFlat, Vector3.UnitY));
        var move = Vector3.Zero;
        if (Down(SDLScancode.W))
        {
            move += forwardFlat;
        }

        if (Down(SDLScancode.S))
        {
            move -= forwardFlat;
        }

        if (Down(SDLScancode.D))
        {
            move += rightFlat;
        }

        if (Down(SDLScancode.A))
        {
            move -= rightFlat;
        }

        if (Down(SDLScancode.Space))
        {
            move += Vector3.UnitY;
        }

        if (Down(SDLScancode.Lctrl) || Down(SDLScancode.Rctrl))
        {
            move -= Vector3.UnitY;
        }

        if (move.LengthSquared() < 1e-8f)
        {
            return;
        }

        move = Vector3.Normalize(move) * (MoveSpeed * deltaSeconds);
        Position += move;
    }
}
