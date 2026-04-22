using System.Numerics;

namespace ShootingEngine.Voxel;

/// <summary>
/// Grid ray traversal in a coordinate system where voxel (0,0,0) occupies world
/// <c>[(0 - size/2) * cell, (1 - size/2) * cell)</c> on each axis (volume centered at world origin).
/// </summary>
public static class VoxelGridRay
{
    public static bool TryFirstSolid(IVoxelVolume volume, in Vector3 originWorld, in Vector3 dirWorld, float cellSize, out int hx, out int hy, out int hz)
    {
        hx = hy = hz = 0;
        if (!TryEnterVolumeGrid(volume, originWorld, dirWorld, cellSize, out Vector3 oGrid, out Vector3 dGrid, out float tEnter))
        {
            return false;
        }

        return TryMarch(volume, in oGrid, in dGrid, tEnter, wantSolid: true, out hx, out hy, out hz);
    }

    public static bool TryFirstAir(IVoxelVolume volume, in Vector3 originWorld, in Vector3 dirWorld, float cellSize, out int hx, out int hy, out int hz)
    {
        hx = hy = hz = 0;
        if (!TryEnterVolumeGrid(volume, originWorld, dirWorld, cellSize, out Vector3 oGrid, out Vector3 dGrid, out float tEnter))
        {
            return false;
        }

        return TryMarch(volume, in oGrid, in dGrid, tEnter, wantSolid: false, out hx, out hy, out hz);
    }

    public static bool TryEnterVolumeGrid(
        IVoxelVolume volume,
        in Vector3 originWorld,
        in Vector3 dirWorld,
        float cellSize,
        out Vector3 oGrid,
        out Vector3 dGrid,
        out float tEnter)
    {
        oGrid = default;
        dGrid = default;
        tEnter = 0f;

        float inv = 1f / cellSize;
        float hx = volume.SizeX * 0.5f;
        float hy = volume.SizeY * 0.5f;
        float hz = volume.SizeZ * 0.5f;

        // Grid float coords: integer voxel indices span [0, Size) matching VoxelMeshBaker mapping.
        Vector3 o = originWorld * inv + new Vector3(hx, hy, hz);
        Vector3 d = dirWorld * inv;
        if (d.LengthSquared() < 1e-12f)
        {
            return false;
        }

        float t0 = 0f;
        float t1 = 1e9f;
        for (int axis = 0; axis < 3; axis++)
        {
            float oa = axis == 0 ? o.X : axis == 1 ? o.Y : o.Z;
            float da = axis == 0 ? d.X : axis == 1 ? d.Y : d.Z;
            float minA = 0f;
            float maxA = axis == 0 ? volume.SizeX : axis == 1 ? volume.SizeY : volume.SizeZ;

            if (MathF.Abs(da) < 1e-12f)
            {
                if (oa < minA || oa >= maxA)
                {
                    return false;
                }

                continue;
            }

            float invDa = 1f / da;
            float tNear = (minA - oa) * invDa;
            float tFar = (maxA - oa) * invDa;
            if (tNear > tFar)
            {
                (tNear, tFar) = (tFar, tNear);
            }

            t0 = MathF.Max(t0, tNear);
            t1 = MathF.Min(t1, tFar);
            if (t0 > t1)
            {
                return false;
            }
        }

        tEnter = t0 >= 0f ? t0 : 0f;
        if (tEnter > t1)
        {
            return false;
        }

        oGrid = o;
        dGrid = d;
        return true;
    }

    private static bool TryMarch(IVoxelVolume volume, in Vector3 oGrid, in Vector3 dGrid, float tEnter, bool wantSolid, out int hx, out int hy, out int hz)
    {
        hx = hy = hz = 0;

        Vector3 p = oGrid + dGrid * (tEnter + 1e-4f);

        int x = (int)MathF.Floor(p.X);
        int y = (int)MathF.Floor(p.Y);
        int z = (int)MathF.Floor(p.Z);

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
            if (x >= 0 && y >= 0 && z >= 0 && x < volume.SizeX && y < volume.SizeY && z < volume.SizeZ)
            {
                VoxelRgbA v = volume.Get(x, y, z);
                bool isAir = v.IsAir;
                bool match = wantSolid ? !isAir : isAir;
                if (match)
                {
                    hx = x;
                    hy = y;
                    hz = z;
                    return true;
                }
            }
            else
            {
                break;
            }

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

