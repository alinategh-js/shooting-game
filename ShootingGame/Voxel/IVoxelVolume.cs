using System.Numerics;

namespace ShootingGame.Voxel;

/// <summary>
/// Read/write access to a finite 3D voxel grid. Today we only ship a dense RGBA implementation,
/// but gameplay/editor code should depend on this interface so we can swap in sparse/chunk storage later.
/// </summary>
public interface IVoxelVolume
{
    int SizeX { get; }
    int SizeY { get; }
    int SizeZ { get; }

    VoxelRgbA Get(int x, int y, int z);
    void Set(int x, int y, int z, in VoxelRgbA voxel);

    bool InBounds(int x, int y, int z);
}
