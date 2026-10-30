using tPoseidon.Core.Game.Quests;

namespace tPoseidon.Core.Net.Dialogs;

public static class NpcFunctionFrames
{
    public const ushort CardBattleRankNotice = 0x1D5A;
    public const ushort GuildSetupWindow = 0x021F;
    public const ushort EventTalkStart = 0x0444;
    public const ushort EventTalkEnd = 0x0445;
    public const ushort EventTalkEnded = 0x0446;
    public const ushort EventSceneStart = 0x04E0;
    public const ushort EventSceneEnd = 0x04E1;
    public const ushort EventSceneEnded = 0x04E2;
    public const ushort WarpGateClose = 0x01EC;
    public const ushort ZoneCommand = 0x03AC;
    public const string ReEnterRemainCommand = "CMD_PPZ_RE_ENTER_REMAIN";
    public const string ZoneClosingCommand = "CMD_ZONECLOSING_NOTIFY";

    public static ushort? GuildWindow(int functionId) => functionId switch
    {
        54 => 0x023F,
        55 => 0x023D,
        56 => 0x023E,
        57 => 0x0240,
        58 => 0x0241,
        79 => 0x0307,
        80 => 0x0308,
        81 => 0x0309,
        _ => null,
    };

    public static ushort EventStartOpcode(int kind) => kind == NpcEventTalk.Scene ? EventSceneStart : EventTalkStart;

    public static ushort EventEndedOpcode(int kind) => kind == NpcEventTalk.Scene ? EventSceneEnded : EventTalkEnded;

    public static int? EventKindOfEnd(ushort opcode) => opcode switch
    {
        EventTalkEnd => NpcEventTalk.Talk,
        EventSceneEnd => NpcEventTalk.Scene,
        _ => null,
    };

    public static byte[] ReEnterRemain(int minutes) => ZoneCommandBody(ReEnterRemainCommand, minutes);

    public static byte[] ZoneClosingNotify(int secondsLeft) => ZoneCommandBody(ZoneClosingCommand, secondsLeft);

    private static byte[] ZoneCommandBody(string command, int value)
    {
        var w = new tPoseidon.Core.Net.PacketWriter(command.Length + 5);
        w.WriteNulTerminated(command, System.Text.Encoding.ASCII);
        w.WriteUInt32((uint)Math.Max(0, value));
        return w.ToArray();
    }

    public static byte[] GuildSetup(long money, int minGelder)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32Field(money);
        w.WriteUInt32((uint)minGelder);
        return w.ToArray();
    }

    public static byte[] EventStart(int npcChrType)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32((uint)npcChrType);
        return w.ToArray();
    }

    public static byte[] EventEnded(ushort npcEntityId, ushort message)
    {
        var w = new PacketWriter(5);
        w.WriteByte(1);
        w.WriteUInt16(npcEntityId);
        w.WriteUInt16(message);
        return w.ToArray();
    }

    public static byte[] EventEndedWithoutTalk() => new byte[] { 0 };
}
