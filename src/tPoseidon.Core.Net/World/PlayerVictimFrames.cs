using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class PlayerVictimFrames
{
    public static IReadOnlyList<SwingFrame> Wrap(
        IReadOnlyList<SwingFrame> monsterVictimFrames,
        IReadOnlyList<SwingFrame> beforeHit,
        IReadOnlyList<SwingFrame> afterHit)
    {
        ArgumentNullException.ThrowIfNull(monsterVictimFrames);
        ArgumentNullException.ThrowIfNull(beforeHit);
        ArgumentNullException.ThrowIfNull(afterHit);
        var frames = new List<SwingFrame>(monsterVictimFrames.Count + beforeHit.Count + afterHit.Count);
        foreach (var frame in monsterVictimFrames)
        {
            if (frame.Opcode is Opcode.S2C_OP_011E_TBD or Opcode.S2C_OP_002B_TBD) continue;
            if (frame.Opcode == Opcode.S2C_OP_002A_TBD)
            {
                frames.AddRange(beforeHit);
                frames.Add(frame);
                frames.AddRange(afterHit);
                continue;
            }
            frames.Add(frame);
        }
        return frames;
    }
}
