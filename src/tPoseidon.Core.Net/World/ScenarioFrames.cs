using System.Text;

namespace tPoseidon.Core.Net.World;

public static class ScenarioFrames
{
    public static byte[] AllMapSkill(uint monsterId, uint skillId, string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(8 + text.Length + 1);
        w.WriteUInt32(monsterId);
        w.WriteUInt32(skillId);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] Notice(string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(text.Length + 1);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] Outcome(bool won) => new[] { won ? (byte)1 : (byte)0 };
}
