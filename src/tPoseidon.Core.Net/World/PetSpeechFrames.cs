using System.Text;
using tPoseidon.Core.Game.Pets;

namespace tPoseidon.Core.Net.World;

public static class PetSpeechFrames
{
    public static byte[] Speech(ushort petEntityId, PetUtterance utterance, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(textEncoding);
        var w = new PacketWriter(sizeof(ushort) + textEncoding.GetByteCount(utterance.Text) + 1 + sizeof(int));
        w.WriteUInt16(petEntityId);
        w.WriteNulTerminated(utterance.Text, textEncoding);
        w.WriteInt32(utterance.Color);
        return w.ToArray();
    }
}
