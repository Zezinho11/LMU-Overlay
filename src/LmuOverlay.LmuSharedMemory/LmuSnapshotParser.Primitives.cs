using System.Globalization;
using LmuOverlay.Domain;

namespace LmuOverlay.LmuSharedMemory;

public static partial class LmuSnapshotParser
{
    private static LmuSessionKind ToSessionKind(int sessionCode) =>
        sessionCode switch
        {
            0 => LmuSessionKind.TestDay,
            >= 1 and <= 4 => LmuSessionKind.Practice,
            >= 5 and <= 8 => LmuSessionKind.Qualifying,
            9 => LmuSessionKind.Warmup,
            >= 10 and <= 13 => LmuSessionKind.Race,
            _ => LmuSessionKind.Unknown
        };

    private static bool ReadBoolean(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadBoolean(data, offset);

    private static short ReadInt16(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadInt16(data, offset);

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadInt32(data, offset);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadUInt32(data, offset);

    private static float ReadSingle(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadSingle(data, offset);

    private static double ReadDouble(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadDouble(data, offset);

    private static LmuVector3 ReadVector3(ReadOnlySpan<byte> data, int offset) =>
        LmuBinaryReader.ReadVector3(data, offset);

    private static string ReadText(ReadOnlySpan<byte> data, int offset, int length) =>
        LmuBinaryReader.ReadText(data, offset, length);
}
