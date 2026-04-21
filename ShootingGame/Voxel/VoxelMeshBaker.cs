using System.Numerics;
using Vortice.Mathematics;

namespace ShootingGame.Voxel;

/// <summary>
/// Converts an <see cref="IVoxelVolume"/> into draw instances (unit cubes). This is the main seam
/// for future optimizations (chunking, greedy meshing, instancing buffers) without touching editor UI code.
/// </summary>
public sealed class VoxelMeshBaker
{
    public const int MaxInstancesDefault = 250_000;

    public int MaxInstances { get; set; } = MaxInstancesDefault;

    public bool TruncatedLastBuild { get; private set; }

    public static Vector3 CellCenterWorld(int x, int y, int z, int sizeX, int sizeY, int sizeZ, float cellSize)
    {
        float hx = sizeX * 0.5f;
        float hy = sizeY * 0.5f;
        float hz = sizeZ * 0.5f;
        return new Vector3(
            (x - hx + 0.5f) * cellSize,
            (y - hy + 0.5f) * cellSize,
            (z - hz + 0.5f) * cellSize);
    }

    public void Rebuild(IVoxelVolume volume, float cellSize, List<SceneInstance> outInstances)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(outInstances);

        outInstances.Clear();
        TruncatedLastBuild = false;

                    int count = 0;
                    for (int z = 0; z < volume.SizeZ; z++)
                    {
                        for (int y = 0; y < volume.SizeY; y++)
                        {
                            for (int x = 0; x < volume.SizeX; x++)
                            {
                                VoxelRgbA v = volume.Get(x, y, z);
                                if (v.IsAir)
                                {
                                    continue;
                                }

                                if (count >= MaxInstances)
                                {
                                    TruncatedLastBuild = true;
                                    return;
                                }

                                Vector3 center = CellCenterWorld(x, y, z, volume.SizeX, volume.SizeY, volume.SizeZ, cellSize);

                    Matrix4x4 world = Matrix4x4.Multiply(
                        Matrix4x4.CreateScale(cellSize, cellSize, cellSize),
                        Matrix4x4.CreateTranslation(center));

                    float inv255 = 1f / 255f;
                    var tint = new Color4(v.R * inv255, v.G * inv255, v.B * inv255, 1f);
                    outInstances.Add(new SceneInstance(world, tint));
                    count++;
                }
            }
        }
    }
}
