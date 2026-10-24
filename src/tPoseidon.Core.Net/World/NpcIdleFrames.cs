using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public static class NpcIdleFrames
{
    public static IReadOnlyList<(Opcode Opcode, byte[] Payload)> Encode(NpcIdleEvent idleEvent, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(idleEvent);
        ArgumentNullException.ThrowIfNull(textEncoding);

        switch (idleEvent)
        {
            case NpcFacingEvent facing:
                return new[]
                {
                    (Opcode.S2C_NPC_STATE, new NpcStatePacket
                    {
                        EntityId = facing.EntityId,
                        State = facing.Direction,
                    }.EncodePayload()),
                };

            case NpcMotionEvent motion:
                var motionFrame = (Opcode.S2C_OP_01CF_TBD, new Packet01CFTbd
                {
                    EntityId = motion.EntityId,
                    Action = (byte)motion.Step.Action,
                    AniTimeSeconds = (ushort)motion.Step.AniTime,
                    Loop = (byte)motion.Step.Loop,
                }.EncodePayload());
                if (motion.Step.Speech.Length == 0)
                {
                    return new[] { motionFrame };
                }
                return new[]
                {
                    motionFrame,
                    (Opcode.S2C_OP_01D0_TBD, new Packet01D0Tbd
                    {
                        EntityId = motion.EntityId,
                        Speech = motion.Step.Speech,
                    }.EncodePayload(textEncoding)),
                };

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(idleEvent), idleEvent.GetType().Name, "Unknown NPC idle event.");
        }
    }
}
