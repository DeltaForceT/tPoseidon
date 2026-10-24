using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class PostureInput
{
    public static bool IsSwallowed(Opcode opcode) => opcode is
        Opcode.ENTITY_MOVE
        or Opcode.C2S_OP_001D_TBD
        or Opcode.C2S_ITEM_PICKUP
        or Opcode.C2S_DRILL_START
        or Opcode.C2S_OP_0266_TBD;

    public static bool IsSkillRequest(Opcode opcode) => opcode == Opcode.C2S_OP_00EE_TBD;
}
