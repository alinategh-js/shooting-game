using System.Numerics;

namespace ShootingEngine.Voxel;

/// <summary>
/// Computes where a 1×1×1 voxel should be placed from a view ray (last air cell before the first solid),
/// matching the usual "stack on surface / branch sideways" voxel editor behavior.
/// </summary>
public static class VoxelPlacementRay
{
    public static bool TryGetDrawPlacement(IVoxelVolume volume, in Vector3 originWorld, in Vector3 dirWorld, float cellSize, out int px, out int py, out int pz)
    {
        px = py = pz = 0;
        if (!VoxelGridRay.TryEnterVolumeGrid(volume, originWorld, dirWorld, cellSize, out Vector3 oGrid, out Vector3 dGrid, out float tEnter))
        {
            return false;
        }

        Vector3 p = oGrid + dGrid * (tEnter + 1e-4f);
        int x = (int)MathF.Floor(p.X);
        int y = (int)MathF.Floor(p.Y);
        int z = (int)MathF.Floor(p.Z);

        if (!volume.InBounds(x, y, z))
        {
            return false;
        }

        if (!volume.Get(x, y, z).IsAir)
        {
            return false;
        }

        // Fallback: if the ray never hits any solid voxel (e.g. empty volume),
        // allow placing into the first cell the ray enters.
        int entryX = x;
        int entryY = y;
        int entryZ = z;

        bool haveLast = true;
        int lastX = x;
        int lastY = y;
        int lastZ = z;

        float dx = dGrid.X;
        float dy = dGrid.Y;
        float dz = dGrid.Z;

        int stepX = dx >= 0f ? 1 : -1;
        int stepY = dy >= 0f ? 1 : -1;
        int stepZ = dz >= 0f ? 1 : -1;

        float tDeltaX = MathF.Abs(dx) > 1e-12f ? 1f / MathF.Abs(dx) : 1e30f;
        float tDeltaY = MathF.Abs(dy) > 1e-12f ? 1f / MathF.Abs(dy) : 1e30f;
        float tDeltaZ = MathF.Abs(dz) > 1e-12f ? 1f / MathF.Abs(dz) : 1e30f;

        float fracX = p.X - MathF.Floor(p.X);
        float fracY = p.Y - MathF.Floor(p.Y);
        float fracZ = p.Z - MathF.Floor(p.Z);

        float tMaxX = dx > 0f ? (1f - fracX) * tDeltaX : fracX * tDeltaX;
        float tMaxY = dy > 0f ? (1f - fracY) * tDeltaY : fracY * tDeltaY;
        float tMaxZ = dz > 0f ? (1f - fracZ) * tDeltaZ : fracZ * tDeltaZ;

        int maxSteps = volume.SizeX + volume.SizeY + volume.SizeZ + 32;

        for (int i = 0; i < maxSteps; i++)
        {
            if (x < 0 || y < 0 || z < 0 || x >= volume.SizeX || y >= volume.SizeY || z >= volume.SizeZ)
            {
                px = entryX;
                py = entryY;
                pz = entryZ;
                return true;
            }

            VoxelRgbA v = volume.Get(x, y, z);
            if (!v.IsAir)
            {
                if (haveLast)
                {
                    px = lastX;
                    py = lastY;
                    pz = lastZ;
                    return true;
                }

                return false;
            }

            lastX = x;
            lastY = y;
            lastZ = z;
            haveLast = true;

            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
        }

        return false;
    }
}

