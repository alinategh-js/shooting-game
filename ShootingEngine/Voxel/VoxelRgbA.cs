using System.Runtime.InteropServices;

namespace ShootingEngine.Voxel;

[StructLayout(LayoutKind.Sequential)]
public struct VoxelRgbA : IEquatable<VoxelRgbA>
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public static VoxelRgbA Air => new(0, 0, 0, 0);

    public VoxelRgbA(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public readonly bool IsAir => A == 0;

    public static bool operator ==(in VoxelRgbA a, in VoxelRgbA b) => a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

    public static bool operator !=(in VoxelRgbA a, in VoxelRgbA b) => !(a == b);

    public bool Equals(VoxelRgbA other) => this == other;

    public override bool Equals(object? obj) => obj is VoxelRgbA other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
}

