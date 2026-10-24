namespace tPoseidon.Kr2014.Common;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1707",
    Justification = "Underscore-cased opcode names mirror protocol conventions.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design", "CA1028",
    Justification = "Protocol opcodes are u16 on the wire — underlying type must be ushort.")]
public enum Kr2014Opcode : ushort
{
        C2S_NPC_DIALOG_RESPONSE = 0x039F,

        C2S_KR_CHARCREATE_QUERY_07F6 = 0x07F6,

        C2S_KR_CHARCREATE_QUERY_0817 = 0x0817,

        S2C_KR_CHARCREATE_REPLY_07F7 = 0x07F7,

        S2C_KR_CHARSLOT_COUPON_COUNT = 0x07F8,

        S2C_KR_CHARSELECT_WAIT_CLEAR = 0x0818,

    S2C_KR_COMBAT_ENTER = 0x2156,
}
