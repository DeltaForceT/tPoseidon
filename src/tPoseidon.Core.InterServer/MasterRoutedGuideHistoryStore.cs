using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedGuideHistoryStore : IGuideHistoryStore
{
    private static readonly HashSet<InterServerOpcode> LoadAckOpcodes = new()
    {
        InterServerOpcode.TpGuideHistoryLoadAckOk,
        InterServerOpcode.TpGuideHistoryLoadAckCancel,
    };
    private static readonly HashSet<InterServerOpcode> UpsertSystemAckOpcodes = new()
    {
        InterServerOpcode.TpGuideHistoryUpsertSystemAckOk,
        InterServerOpcode.TpGuideHistoryUpsertSystemAckCancel,
    };
    private static readonly HashSet<InterServerOpcode> UpsertTutorialAckOpcodes = new()
    {
        InterServerOpcode.TpGuideHistoryUpsertTutorialAckOk,
        InterServerOpcode.TpGuideHistoryUpsertTutorialAckCancel,
    };

    private readonly MasterServerClient _client;
    private readonly IGuideHistoryStore _fallback;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public MasterRoutedGuideHistoryStore(
        MasterServerClient client,
        IGuideHistoryStore fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _client = client;
        _fallback = fallback;
        _selfIndex = selfIndex;
        _log = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public GuideHistory? GetForCharacter(ulong characterId)
    {
        if (!_client.IsConnected)
        {
            _log($"guide-history Load char 0x{characterId:X16}: master not connected; using local fallback");
            return _fallback.GetForCharacter(characterId);
        }

        try
        {
            var requestId = _client.AllocateRequestId();
            var request = new InterServerFrame
            {
                Opcode = InterServerOpcode.TpGuideHistoryLoad,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = requestId,
                },
                Body = Array.Empty<byte>(),
            };
            var reply = _client
                .SendAndAwaitReplyAsync(request, LoadAckOpcodes, _timeout, CancellationToken.None)
                .GetAwaiter().GetResult();

            if (reply.Opcode == InterServerOpcode.TpGuideHistoryLoadAckCancel)
            {
                _log($"guide-history Load char 0x{characterId:X16}: master replied CANCEL; using local fallback");
                return _fallback.GetForCharacter(characterId);
            }

            var ack = GuideHistoryLoadAckOk.DecodeBody(reply.Body);
            return new GuideHistory
            {
                CharacterId = characterId,
                SystemGuide = ack.SystemGuide,
                TutorialGuide = ack.TutorialGuide,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"guide-history Load char 0x{characterId:X16}: master round-trip failed ({ex.GetType().Name}: {ex.Message}); using local fallback");
            return _fallback.GetForCharacter(characterId);
        }
    }

    public void UpsertSystemGuide(ulong characterId, string systemGuide)
    {
        ArgumentNullException.ThrowIfNull(systemGuide);
        if (!_client.IsConnected)
        {
            _log($"guide-history UpsertSystem char 0x{characterId:X16}: master not connected; using local fallback");
            _fallback.UpsertSystemGuide(characterId, systemGuide);
            return;
        }

        try
        {
            var body = new GuideHistoryUpsertSystemRequest { SystemGuide = systemGuide }.EncodeBody();
            var requestId = _client.AllocateRequestId();
            var request = new InterServerFrame
            {
                Opcode = InterServerOpcode.TpGuideHistoryUpsertSystem,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = requestId,
                },
                Body = body,
            };
            _client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"guide-history UpsertSystem char 0x{characterId:X16}: master send failed ({ex.GetType().Name}: {ex.Message}); using local fallback");
            _fallback.UpsertSystemGuide(characterId, systemGuide);
        }
    }

    public void UpsertTutorialGuide(ulong characterId, ulong tutorialGuide)
    {
        if (!_client.IsConnected)
        {
            _log($"guide-history UpsertTutorial char 0x{characterId:X16}: master not connected; using local fallback");
            _fallback.UpsertTutorialGuide(characterId, tutorialGuide);
            return;
        }

        try
        {
            var body = new GuideHistoryUpsertTutorialRequest { TutorialGuide = tutorialGuide }.EncodeBody();
            var requestId = _client.AllocateRequestId();
            var request = new InterServerFrame
            {
                Opcode = InterServerOpcode.TpGuideHistoryUpsertTutorial,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = characterId,
                    RequestId = requestId,
                },
                Body = body,
            };
            _client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"guide-history UpsertTutorial char 0x{characterId:X16}: master send failed ({ex.GetType().Name}: {ex.Message}); using local fallback");
            _fallback.UpsertTutorialGuide(characterId, tutorialGuide);
        }
    }
}
