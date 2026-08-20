using System.Buffers.Binary;
using System.Text;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

/// <summary>Little-endian primitives for the LMU shared-memory ABI.</summary>
internal static class LmuBinaryReader
{
    internal static bool ReadBoolean(ReadOnlySpan<byte> data, int offset) => data[offset] != 0;
    internal static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data.Slice(offset, sizeof(short)));
    internal static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, sizeof(int)));
    internal static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
    internal static float ReadSingle(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, sizeof(float)));
    internal static double ReadDouble(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadDoubleLittleEndian(data.Slice(offset, sizeof(double)));
    internal static LmuVector3 ReadVector3(ReadOnlySpan<byte> data, int offset) => new(
        ReadDouble(data, offset),
        ReadDouble(data, offset + sizeof(double)),
        ReadDouble(data, offset + (2 * sizeof(double))));
    internal static string ReadText(ReadOnlySpan<byte> data, int offset, int length)
    {
        var field = data.Slice(offset, length);
        var terminator = field.IndexOf((byte)0);
        if (terminator >= 0) field = field[..terminator];
        return Encoding.UTF8.GetString(field).Trim();
    }
}
