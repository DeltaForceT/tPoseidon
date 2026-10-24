using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.InterServer;

public sealed class MasterPartyQuests : IPartyQuestMaster
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterPartyQuests(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<PartyQuestStart?> AcceptAsync(ulong characterId, uint questId, ushort monsterId, ushort targetCount, IReadOnlyList<ulong> members, CancellationToken ct)
    {
        var reply = await CallAsync(characterId, InterServerOpcode.QcmdAcceptPartyQuest, InterServerOpcode.QcmdAcceptPartyQuestAckOk, InterServerOpcode.QcmdAcceptPartyQuestAckCancel,
            PartyQuestMessages.EncodeAccept(new PartyQuestAcceptRequest(questId, monsterId, targetCount, members)), ct).ConfigureAwait(false);
        if (reply is null) return null;
        var accepted = PartyQuestMessages.DecodeAccepted(reply);
        return new PartyQuestStart(accepted.PartyUid, accepted.CompletionCounts);
    }

    public async Task<bool> CompleteAsync(ulong characterId, uint questId, ulong partyUid, ushort killCount, int recordTime, byte memberCount, CancellationToken ct) =>
        await CallAsync(characterId, InterServerOpcode.QcmdInsertPartyQuest, InterServerOpcode.QcmdInsertPartyQuestAckOk, InterServerOpcode.QcmdInsertPartyQuestAckCancel,
            PartyQuestMessages.EncodeComplete(new PartyQuestCompleteRequest(questId, partyUid, killCount, recordTime, memberCount)), ct).ConfigureAwait(false) is not null;

    public async Task<int?> RewardAsync(ulong characterId, ulong partyUid, uint questId, CancellationToken ct)
    {
        var reply = await CallAsync(characterId, InterServerOpcode.QcmdCompletePartyQuest, InterServerOpcode.QcmdCompletePartyQuestAckOk, InterServerOpcode.QcmdCompletePartyQuestAckCancel,
            PartyQuestMessages.EncodeReward(new PartyQuestRewardRequest(partyUid, questId)), ct).ConfigureAwait(false);
        return reply is null ? null : PartyQuestMessages.DecodeCompletionCount(reply);
    }

    public async Task<IReadOnlyList<PartyQuestRecord>> ListAsync(ulong characterId, CancellationToken ct)
    {
        var reply = await CallAsync(characterId, InterServerOpcode.QcmdSelectPartyQuest, InterServerOpcode.QcmdSelectPartyQuestAckOk, InterServerOpcode.QcmdSelectPartyQuestAckCancel,
            Array.Empty<byte>(), ct).ConfigureAwait(false);
        return reply is null ? Array.Empty<PartyQuestRecord>() : PartyQuestMessages.DecodeList(characterId, reply);
    }

    private async Task<byte[]?> CallAsync(ulong characterId, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode cancel, byte[] body, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[party-quest] Master not connected; 0x{(ushort)request:X4} for 0x{characterId:X} not sent");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = request,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = body,
            }, new HashSet<InterServerOpcode> { ok, cancel }, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == ok ? reply.Body : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[party-quest] Master did not answer 0x{(ushort)request:X4} for 0x{characterId:X}");
            return null;
        }
    }
}
