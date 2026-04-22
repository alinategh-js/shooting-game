namespace ShootingEngine.Voxel;

/// <summary>
/// Dense RGBA storage. Alpha 0 means air. This is intentionally simple for MVP IO and tooling.
/// </summary>
public sealed class DenseRgbVoxelVolume : IVoxelVolume
{
    private readonly VoxelRgbA[] _voxels;

    public DenseRgbVoxelVolume(int sizeX, int sizeY, int sizeZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeX), "Volume dimensions must be positive.");
        }

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        _voxels = new VoxelRgbA[checked(SizeX * SizeY * SizeZ)];
    }

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }

    public VoxelRgbA Get(int x, int y, int z)
    {
        if (!InBounds(x, y, z))
        {
            return VoxelRgbA.Air;
        }

        return _voxels[Index(x, y, z)];
    }

    public void Set(int x, int y, int z, in VoxelRgbA voxel)
    {
        if (!InBounds(x, y, z))
        {
            return;
        }

        _voxels[Index(x, y, z)] = voxel;
    }

    public ReadOnlySpan<VoxelRgbA> AsSpan() => _voxels;

    public Span<VoxelRgbA> AsWritableSpan() => _voxels;

    public void Clear(in VoxelRgbA value = default)
    {
        _voxels.AsSpan().Fill(value);
    }

    public bool InBounds(int x, int y, int z) =>
        (uint)x < (uint)SizeX && (uint)y < (uint)SizeY && (uint)z < (uint)SizeZ;

    public int Index(int x, int y, int z) => x + (SizeX * (y + (SizeY * z)));

    public static DenseRgbVoxelVolume CreateFilledCube(int size, in VoxelRgbA fill)
    {
        var v = new DenseRgbVoxelVolume(size, size, size);
        v.Clear(fill);
        return v;
    }
}

