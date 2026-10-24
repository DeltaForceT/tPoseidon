using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.S2;

public interface IAutoAcceptedQuestEmitter
{
        Task AutoAcceptAsync(S2SessionContext ctx, Character character, int questId, CancellationToken ct);
}
