using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Dialogs;

public interface INpcDialogService
{
        IReadOnlyList<DialogPair> GetSpawnDialogs(Character character);
}

public sealed record DialogPair(NpcDialogPacket Opener, NpcDialogFollowupPacket Followup);

public sealed class NpcDialogService : INpcDialogService
{
        public const ushort CoralTownMapId = SpeciesDefaults.CoralTownMapId;

    public IReadOnlyList<DialogPair> GetSpawnDialogs(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return Array.Empty<DialogPair>();
    }
}
