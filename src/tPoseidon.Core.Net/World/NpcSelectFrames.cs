using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class NpcSelectFrames
{
    public static (Opcode Opcode, byte[] Payload) Cancel() => (Opcode.S2C_OP_0071_TBD, Array.Empty<byte>());
}
