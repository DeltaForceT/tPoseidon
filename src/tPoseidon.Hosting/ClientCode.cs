using System.Buffers.Binary;

namespace tPoseidon.Hosting;

internal static class ClientCode
{
    public const byte Nop = 0x90;
    public const byte Padding = 0xCC;
    public const byte Call = 0xE8;
    public const byte Jump = 0xE9;
    public const int BranchLength = 5;

    public static byte?[] Pattern(string hex) =>
        hex.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token == "??" ? (byte?)null : Convert.ToByte(token, 16))
            .ToArray();

    public static int FindUnique(ReadOnlySpan<byte> executable, byte?[] pattern)
    {
        var found = -1;
        for (var start = 0; start + pattern.Length <= executable.Length; start++)
        {
            if (!MatchesAt(executable, start, pattern))
            {
                continue;
            }

            if (found >= 0)
            {
                return -1;
            }

            found = start;
        }

        return found;
    }

    public static bool MatchesAt(ReadOnlySpan<byte> executable, int offset, byte?[] pattern)
    {
        if (offset < 0 || offset + pattern.Length > executable.Length)
        {
            return false;
        }

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] is { } expected && executable[offset + i] != expected)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsBranch(ReadOnlySpan<byte> executable, int offset, byte opcode, int target) =>
        offset >= 0
        && offset + BranchLength <= executable.Length
        && executable[offset] == opcode
        && offset + BranchLength + BinaryPrimitives.ReadInt32LittleEndian(executable.Slice(offset + 1, 4)) == target;

    public static void WriteBranch(Span<byte> executable, int offset, byte opcode, int target)
    {
        executable[offset] = opcode;
        BinaryPrimitives.WriteInt32LittleEndian(executable.Slice(offset + 1, 4), target - (offset + BranchLength));
    }

    public static bool IsFilled(ReadOnlySpan<byte> executable, int offset, int length, byte value) =>
        offset >= 0 && offset + length <= executable.Length && !executable.Slice(offset, length).ContainsAnyExcept(value);
}
