using System.Numerics;
using Hexa.NET.SDL2;

namespace ShootingGame;

/// <summary>
/// FPS look from the eyes + feet-based locomotion with collision (no flying).
/// </summary>
public struct FpsCamera
{
    public const float MouseSensitivity = 0.0022f;
    public const float StandWalkSpeed = 4.35f;
    public const float CrouchWalkSpeed = 2.35f;
    public const float AirWishSpeed = 3.05f;
    public const float AirAccel = 16f;
    public const float GroundFriction = 14f;

    public const float StandEyeHeight = 1.62f;
    public const float CrouchEyeHeight = 0.95f;

    public Vector3 FeetPosition;
    public float Yaw;
    public float Pitch;
    public Vector3 Velocity;
    public bool IsCrouching;

    public static FpsCamera CreateDefault()
    {
        return new FpsCamera
        {
            FeetPosition = new Vector3(0f, 0f, -1.8f),
            Yaw = 0f,
            Pitch = -0.06f,
            Velocity = Vector3.Zero,
        };
    }

    public readonly float EyeHeight(bool crouching) => crouching ? CrouchEyeHeight : StandEyeHeight;

    public readonly Vector3 EyePosition(bool crouching) =>
        FeetPosition + new Vector3(0f, EyeHeight(crouching), 0f);

    public void ApplyMouseLook(int deltaX, int deltaY)
    {
        Yaw -= deltaX * MouseSensitivity;
        Pitch -= deltaY * MouseSensitivity;
        float limit = MathF.PI / 2f - 0.01f;
        Pitch = Math.Clamp(Pitch, -limit, limit);
    }

    public readonly Matrix4x4 GetViewMatrix(bool crouching)
    {
        Vector3 eye = EyePosition(crouching);
        GetLookDirection(out Vector3 forward);
        Vector3 target = eye + forward;
        return Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY);
    }

    public readonly void GetLookDirection(out Vector3 forward)
    {
        float cp = MathF.Cos(Pitch);
        float sp = MathF.Sin(Pitch);
        float cy = MathF.Cos(Yaw);
        float sy = MathF.Sin(Yaw);
        forward = Vector3.Normalize(new Vector3(sy * cp, sp, cy * cp));
    }

    public readonly void GetPlanarBasis(out Vector3 forwardFlat, out Vector3 rightFlat)
    {
        float cy = MathF.Cos(Yaw);
        float sy = MathF.Sin(Yaw);
        forwardFlat = new Vector3(sy, 0f, cy);
        if (forwardFlat.LengthSquared() > 1e-8f)
        {
            forwardFlat = Vector3.Normalize(forwardFlat);
        }

        rightFlat = Vector3.Normalize(Vector3.Cross(forwardFlat, Vector3.UnitY));
    }

    public unsafe void UpdateLocomotion(
        float deltaSeconds,
        byte* keys,
        int keyCount,
        ReadOnlySpan<Aabb> world,
        ref bool isGrounded)
    {
        bool Down(SDLScancode sc)
        {
            int i = (int)sc;
            return i >= 0 && i < keyCount && keys[i] != 0;
        }

        IsCrouching = Down(SDLScancode.Lctrl) || Down(SDLScancode.Rctrl);
        var dims = IsCrouching ? PlayerColliderDims.Crouching : PlayerColliderDims.Standing;

        bool jumpPressed = Down(SDLScancode.Space);

        GetPlanarBasis(out Vector3 forwardFlat, out Vector3 rightFlat);
        var wish = Vector3.Zero;
        if (Down(SDLScancode.W))
        {
            wish += forwardFlat;
        }

        if (Down(SDLScancode.S))
        {
            wish -= forwardFlat;
        }

        if (Down(SDLScancode.D))
        {
            wish += rightFlat;
        }

        if (Down(SDLScancode.A))
        {
            wish -= rightFlat;
        }

        if (wish.LengthSquared() > 1e-8f)
        {
            wish = Vector3.Normalize(wish);
        }

        bool onGround = isGrounded;
        if (onGround)
        {
            float speed = IsCrouching ? CrouchWalkSpeed : StandWalkSpeed;
            if (wish.LengthSquared() > 1e-8f)
            {
                Velocity = new Vector3(wish.X * speed, Velocity.Y, wish.Z * speed);
            }
            else
            {
                float fr = Math.Clamp(GroundFriction * deltaSeconds, 0f, 0.92f);
                float vx = Velocity.X * (1f - fr);
                float vz = Velocity.Z * (1f - fr);
                Velocity = new Vector3(vx, Velocity.Y, vz);
            }
        }
        else
        {
            if (wish.LengthSquared() > 1e-8f)
            {
                Vector3 horiz = new(Velocity.X, 0f, Velocity.Z);
                horiz += wish * (AirAccel * deltaSeconds);
                float len = horiz.Length();
                if (len > AirWishSpeed && len > 1e-6f)
                {
                    horiz *= AirWishSpeed / len;
                }

                Velocity = new Vector3(horiz.X, Velocity.Y, horiz.Z);
            }
        }

        PlayerCollision.IntegrateAndResolve(
            ref FeetPosition,
            ref Velocity,
            in dims,
            world,
            deltaSeconds,
            jumpPressed,
            ref isGrounded);

    }

    /// <summary>Right hand (palm) in world space — single scaled cube, no external mesh.</summary>
    public readonly Matrix4x4 GetRightHandWorld(bool crouching, float simTimeSeconds)
    {
        Vector3 eye = EyePosition(crouching);
        GetLookDirection(out Vector3 forward);
        GetPlanarBasis(out _, out Vector3 right);
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));
        float bob = MathF.Sin(simTimeSeconds * 9f) * 0.012f;
        float sway = MathF.Sin(simTimeSeconds * 4.3f) * 0.008f;
        float crouchMul = crouching ? 0.88f : 1f;
        Vector3 palm = eye
            + forward * (0.48f * crouchMul)
            + right * (0.34f + sway)
            - up * (0.24f * crouchMul + bob);

        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, Pitch, sway * 6f);
        Matrix4x4 scl = Matrix4x4.CreateScale(0.13f, 0.11f, 0.1f);
        return Matrix4x4.Multiply(scl, Matrix4x4.Multiply(rot, Matrix4x4.CreateTranslation(palm)));
    }
}
