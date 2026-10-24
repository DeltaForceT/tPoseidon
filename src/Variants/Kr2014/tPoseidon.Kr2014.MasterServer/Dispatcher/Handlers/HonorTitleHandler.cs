using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class HonorTitleHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpHonorTitleLoad,
        InterServerOpcode.McmdHonorTitleAdd,
        InterServerOpcode.McmdProfileTitle,
    };

    private readonly IHonorTitleStore _store;

    public HonorTitleHandler(IHonorTitleStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        (InterServerOpcode Opcode, byte[] Body)? reply;
        try
        {
            reply = frame.Opcode switch
            {
                InterServerOpcode.TpHonorTitleLoad => (InterServerOpcode.TpHonorTitleLoadAckOk, HonorTitleMessages.EncodeLogin(_store.Load(charUid))),
                InterServerOpcode.McmdHonorTitleAdd => Add(charUid, HonorTitleMessages.DecodeAdd(frame.Body), ctx),
                InterServerOpcode.McmdProfileTitle => ProfileTitle(charUid, frame.Body),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"honor title 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = frame.Opcode == InterServerOpcode.TpHonorTitleLoad ? (InterServerOpcode.TpHonorTitleLoadAckCancel, []) : null;
        }
        if (reply is not { } answer) return;
        await session.SendAsync(new InterServerFrame
        {
            Opcode = answer.Opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = answer.Body,
        }, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[])? Add(ulong charUid, HonorTitleAdd add, MasterContext ctx)
    {
        if (!_store.Insert(charUid, add.Titles))
        {
            ctx.Log($"AddHonorTitle char 0x{charUid:X16}: {add.Titles.Count} titles not stored");
            return null;
        }
        ctx.Log($"AddHonorTitle char 0x{charUid:X16}: titles {string.Join(",", add.Titles.Select(title => title.TitleId))}");
        return (InterServerOpcode.McmdHonorTitleAdded,
            HonorTitleMessages.EncodeAdded(new HonorTitleAdded(add.ZoneId, add.EntityId, add.Titles.Select(title => title.TitleId).ToArray())));
    }

    private (InterServerOpcode, byte[])? ProfileTitle(ulong charUid, ReadOnlySpan<byte> body)
    {
        var (titleType, titleId) = HonorTitleMessages.DecodeProfileTitle(body);
        _store.SetProfileTitle(charUid, titleType, unchecked((uint)titleId));
        return null;
    }
}
