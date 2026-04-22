using System.Numerics;
using Hexa.NET.SDL2;
using ShootingEngine.Graphics;
using Vortice.Mathematics;

namespace ShootingGame;

/// <summary>
/// FPS look from the eyes + feet-based locomotion with collision (no flying).
/// </summary>
public struct FpsCamera
{
    public const float MouseSensitivity = 0.0022f;
    public const float StandWalkSpeed = 4.35f;
    public const float CrouchWalkSpeed = 2.35f;
    public const float SprintWalkSpeed = 6.9f;
    public const float AirWishSpeed = 3.05f;
    public const float AirAccel = 16f;
    public const float GroundFriction = 14f;

    public const float StandEyeHeight = 1.62f;
    public const float CrouchEyeHeight = 0.95f;
    public const float CrouchSpeed = 9.5f;

    public Vector3 FeetPosition;
    public float Yaw;
    public float Pitch;
    public Vector3 Velocity;
    public bool IsCrouching;
    public float CrouchAmount;
    public float SmoothCrouchAmount;
    public bool IsSprinting;

    public static FpsCamera CreateDefault()
    {
        return new FpsCamera
        {
            FeetPosition = new Vector3(0f, 0f, -1.8f),
            Yaw = 0f,
            Pitch = -0.06f,
            Velocity = Vector3.Zero,
            CrouchAmount = 0f,
            SmoothCrouchAmount = 0f,
        };
    }

    public readonly float EyeHeight() => Lerp(StandEyeHeight, CrouchEyeHeight, SmoothCrouchAmount);

    public readonly Vector3 EyePosition() => FeetPosition + new Vector3(0f, EyeHeight(), 0f);

    public void ApplyMouseLook(int deltaX, int deltaY)
    {
        Yaw -= deltaX * MouseSensitivity;
        Pitch -= deltaY * MouseSensitivity;
        float limit = MathF.PI / 2f - 0.01f;
        Pitch = Math.Clamp(Pitch, -limit, limit);
    }

    public readonly Matrix4x4 GetViewMatrix()
    {
        Vector3 eye = EyePosition();
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

        bool wantCrouch = Down(SDLScancode.Lctrl) || Down(SDLScancode.Rctrl);
        bool wantSprint = Down(SDLScancode.Lshift) || Down(SDLScancode.Rshift);
        IsSprinting = wantSprint && !wantCrouch;

        // Smooth crouch for camera + collider. Standing up is blocked if we'd intersect the world.
        float target = wantCrouch ? 1f : 0f;
        float step = CrouchSpeed * deltaSeconds;
        float nextCrouch = MoveTowards(CrouchAmount, target, step);
        if (!wantCrouch && nextCrouch < CrouchAmount)
        {
            // Standing up: only allow if no ceiling overlap for the taller capsule.
            var testDims = PlayerColliderDims.WithHeight(Lerp(PlayerColliderDims.Standing.Height, PlayerColliderDims.Crouching.Height, nextCrouch));
            if (IntersectsAny(PlayerCollision.PlayerBounds(FeetPosition, testDims), world))
            {
                nextCrouch = CrouchAmount;
            }
        }

        CrouchAmount = nextCrouch;
        SmoothCrouchAmount = SmoothDamp01(SmoothCrouchAmount, CrouchAmount, 12f, deltaSeconds);

        float height = Lerp(PlayerColliderDims.Standing.Height, PlayerColliderDims.Crouching.Height, CrouchAmount);
        var dims = PlayerColliderDims.WithHeight(height);

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
            float baseSpeed = Lerp(StandWalkSpeed, CrouchWalkSpeed, CrouchAmount);
            float speed = IsSprinting ? SprintWalkSpeed : baseSpeed;
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

    /// <summary>Right hand (low-poly cubes) in world space.</summary>
    public readonly void AppendRightHandInstances(List<SceneInstance> outInstances, float simTimeSeconds)
    {
        Vector3 eye = EyePosition();
        GetLookDirection(out Vector3 forward);
        forward = Vector3.Normalize(forward);

        // True view-right from the look vector (do not mix yaw-only right with pitched forward).
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        if (right.LengthSquared() < 1e-10f)
        {
            right = new Vector3(1f, 0f, 0f);
        }
        else
        {
            right = Vector3.Normalize(right);
        }

        Vector3 up = Vector3.Normalize(Vector3.Cross(right, forward));

        float walkSpeed = new Vector2(Velocity.X, Velocity.Z).Length();
        float walk01 = Math.Clamp(walkSpeed / 5.0f, 0f, 1f);
        float bob = MathF.Sin(simTimeSeconds * (7f + 3f * walk01)) * (0.010f + 0.012f * walk01);
        float sway = MathF.Sin(simTimeSeconds * (3.8f + 1.8f * walk01)) * (0.008f + 0.010f * walk01);
        float idle = MathF.Sin(simTimeSeconds * 1.35f) * 0.006f;
        float c = Lerp(1f, 0.92f, SmoothCrouchAmount);

        // Viewmodel origin in front of camera (kept stable; renderer draws it without depth test).
        const float forwardDist = 0.34f;
        const float rightDist = 0.26f;
        const float downDist = 0.22f;
        Vector3 origin = eye
            + forward * (forwardDist * c)
            + right * (rightDist + sway)
            - up * (downDist * c + bob)
            + forward * (idle);

        // Hand orientation: mostly yaw; a small pitch bias so it follows your view a bit.
        float pitchBias = Pitch * 0.12f;
        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(Yaw, pitchBias, -0.18f + sway * 2.2f);

        Color4 skin0 = new(0.93f, 0.74f, 0.63f, 1f);
        Color4 skin1 = new(0.88f, 0.66f, 0.56f, 1f);
        Color4 nail = new(0.96f, 0.88f, 0.82f, 1f);

        // Forearm + wrist (narrow, arm-like)
        outInstances.Add(new SceneInstance(
            MakeBoxWorld(origin + MulDir(rot, new Vector3(0.00f, -0.02f, -0.18f)), rot, new Vector3(0.050f, 0.045f, 0.20f)),
            skin1));
        outInstances.Add(new SceneInstance(
            MakeBoxWorld(origin + MulDir(rot, new Vector3(0.00f, -0.01f, -0.06f)), rot, new Vector3(0.046f, 0.040f, 0.070f)),
            skin0));

        // Palm (smaller + thinner)
        outInstances.Add(new SceneInstance(MakeBoxWorld(origin, rot, new Vector3(0.075f, 0.042f, 0.060f)), skin0));
        // Back of hand
        outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, new Vector3(0.00f, 0.034f, 0.004f)), rot, new Vector3(0.070f, 0.030f, 0.054f)), skin1));

        // Fingers (index..pinky)
        for (int i = 0; i < 4; i++)
        {
            float x = -0.030f + i * 0.020f;
            float curl = 0.10f + 0.16f * walk01;
            Vector3 baseLocal = new(x, 0.018f, 0.052f);
            Vector3 midLocal = new(x, 0.016f - curl * 0.02f, 0.082f);
            Vector3 tipLocal = new(x, 0.014f - curl * 0.04f, 0.108f);

            outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, baseLocal), rot, new Vector3(0.010f, 0.012f, 0.030f)), skin0));
            outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, midLocal), rot, new Vector3(0.0095f, 0.011f, 0.026f)), skin1));
            outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, tipLocal), rot, new Vector3(0.009f, 0.010f, 0.022f)), skin1));
            outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, tipLocal + new Vector3(0f, 0.010f, 0.014f)), rot, new Vector3(0.008f, 0.006f, 0.010f)), nail));
        }

        // Thumb
        Vector3 thumb0 = new(-0.060f, 0.000f, 0.018f);
        Vector3 thumb1 = new(-0.072f, -0.004f, 0.044f);
        Matrix4x4 thumbRot = Matrix4x4.CreateFromYawPitchRoll(Yaw - 0.30f, pitchBias + 0.05f, -0.95f);
        outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, thumb0), thumbRot, new Vector3(0.010f, 0.011f, 0.030f)), skin1));
        outInstances.Add(new SceneInstance(MakeBoxWorld(origin + MulDir(rot, thumb1), thumbRot, new Vector3(0.009f, 0.010f, 0.024f)), skin0));
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        float d = target - current;
        if (MathF.Abs(d) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(d) * maxDelta;
    }

    private static float SmoothDamp01(float current, float target, float speed, float dt)
    {
        float t = 1f - MathF.Exp(-speed * dt);
        return current + (target - current) * t;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static bool IntersectsAny(in Aabb box, ReadOnlySpan<Aabb> world)
    {
        for (int i = 0; i < world.Length; i++)
        {
            if (Aabb.Intersects(box, world[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 MulDir(in Matrix4x4 m, in Vector3 v)
    {
        return new Vector3(
            v.X * m.M11 + v.Y * m.M21 + v.Z * m.M31,
            v.X * m.M12 + v.Y * m.M22 + v.Z * m.M32,
            v.X * m.M13 + v.Y * m.M23 + v.Z * m.M33);
    }

    private static Matrix4x4 MakeBoxWorld(Vector3 center, in Matrix4x4 rot, Vector3 halfExtents)
    {
        Matrix4x4 scl = Matrix4x4.CreateScale(halfExtents.X * 2f, halfExtents.Y * 2f, halfExtents.Z * 2f);
        Matrix4x4 w = Matrix4x4.Multiply(scl, Matrix4x4.Multiply(rot, Matrix4x4.CreateTranslation(center)));
        return w;
    }
}
