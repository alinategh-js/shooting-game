using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace ShootingEngine.Voxel;

/// <summary>
/// Versioned dense voxel object file (.svxv). Header is fixed-size; payload is raw RGBA bytes in X-fastest order.
/// Future formats can add compression, chunking, or palettes without breaking readers if version bumps.
/// </summary>
public static class SvxvFileFormat
{
    public const uint Magic = 0x56585653; // 'SVXV' little-endian on disk as bytes S V X V

    public const ushort CurrentVersion = 1;

    public const int HeaderByteCount = 4 + 2 + 2 + 2 + 2 + 4; // magic + ver + sx + sy + sz + flags

    public static void Save(string path, DenseRgbVoxelVolume volume, uint flags = 0)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(volume);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        WriteHeader(bw, (ushort)volume.SizeX, (ushort)volume.SizeY, (ushort)volume.SizeZ, flags);

        ReadOnlySpan<VoxelRgbA> data = volume.AsSpan();
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        bw.Write(bytes);
    }

    public static DenseRgbVoxelVolume Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);

        ReadHeader(br, out ushort sx, out ushort sy, out ushort sz, out _);

        long expected = HeaderByteCount + (long)sx * sy * sz * sizeof(uint);
        if (fs.Length < expected)
        {
            throw new InvalidDataException($"SVXV file too small: expected at least {expected} bytes, got {fs.Length}.");
        }

        var vol = new DenseRgbVoxelVolume(sx, sy, sz);
        int byteCount = checked(sx * sy * sz * sizeof(uint));
        byte[] payload = br.ReadBytes(byteCount);
        if (payload.Length != byteCount)
        {
            throw new InvalidDataException("SVXV payload read failed (unexpected EOF).");
        }

        payload.AsSpan().CopyTo(MemoryMarshal.AsBytes(vol.AsWritableSpan()));
        return vol;
    }

    public static void WriteHeader(BinaryWriter bw, ushort sizeX, ushort sizeY, ushort sizeZ, uint flags)
    {
        bw.Write(Magic);
        bw.Write(CurrentVersion);
        bw.Write(sizeX);
        bw.Write(sizeY);
        bw.Write(sizeZ);
        bw.Write(flags);
    }

    public static void ReadHeader(BinaryReader br, out ushort sizeX, out ushort sizeY, out ushort sizeZ, out uint flags)
    {
        uint magic = br.ReadUInt32();
        if (magic != Magic)
        {
            throw new InvalidDataException("Not an SVXV file (bad magic).");
        }

        ushort ver = br.ReadUInt16();
        if (ver != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported SVXV version: {ver}.");
        }

        sizeX = br.ReadUInt16();
        sizeY = br.ReadUInt16();
        sizeZ = br.ReadUInt16();
        flags = br.ReadUInt32();

        if (sizeX == 0 || sizeY == 0 || sizeZ == 0)
        {
            throw new InvalidDataException("Invalid SVXV dimensions.");
        }
    }

    /// <summary>Peek dimensions without reading the whole payload.</summary>
    public static void TryReadDimensions(ReadOnlySpan<byte> headerPrefix, out ushort sizeX, out ushort sizeY, out ushort sizeZ)
    {
        if (headerPrefix.Length < HeaderByteCount)
        {
            throw new InvalidDataException("SVXV header prefix too small.");
        }

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(headerPrefix);
        if (magic != Magic)
        {
            throw new InvalidDataException("Not an SVXV file (bad magic).");
        }

        ushort ver = BinaryPrimitives.ReadUInt16LittleEndian(headerPrefix.Slice(4));
        if (ver != CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported SVXV version: {ver}.");
        }

        sizeX = BinaryPrimitives.ReadUInt16LittleEndian(headerPrefix.Slice(6));
        sizeY = BinaryPrimitives.ReadUInt16LittleEndian(headerPrefix.Slice(8));
        sizeZ = BinaryPrimitives.ReadUInt16LittleEndian(headerPrefix.Slice(10));
    }
}

