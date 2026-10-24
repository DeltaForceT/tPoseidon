using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class ReportSlot
{
    public required string Defendant { get; init; }
    public required ulong DefendantCharUid { get; init; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ushort Kind { get; set; }
    public List<ReportChatLine> Chat { get; } = new();
}

public sealed class ReportServer
{
    private readonly IReportStore _store;
    private readonly SocialRoom _room;
    private readonly Encoding _text;
    private readonly byte _island;
    private readonly Action<string> _log;

    public ReportServer(IReportStore store, SocialRoom room, Encoding text, byte island, Action<string> log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _island = island;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register((Core.Common.Opcode)ReportFrames.SessionOpen, (_, _, _) => Open(ctx));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.TryReport, (_, payload, ct) => TryReportAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.AttachChat, (_, payload, _) => AttachChat(ctx, payload));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.FillReport, (_, payload, _) => Fill(ctx, payload));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.SubmitReport, (_, payload, ct) => SubmitAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.CancelReport, (_, payload, ct) => CancelAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)ReportFrames.CancelQueued, (_, _, _) => Task.CompletedTask);
        dispatcher.Register((Core.Common.Opcode)ReportFrames.IsOnline, (_, payload, ct) => IsOnlineAsync(ctx, payload, ct));
    }

    private Task Open(S2SessionContext ctx)
    {
        if (_room.Find(ctx) is { } member) member.Reports ??= new ReportSlot?[ReportRules.Slots];
        return Task.CompletedTask;
    }

    private async Task TryReportAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { Reports: { } slots } member) return;
        var defendant = ReportFrames.ReadName(payload.Span, _text);
        var name = ReportFrames.Name(defendant, _text);
        if (string.Equals(defendant, member.Character.Name, StringComparison.Ordinal))
        {
            await SendAsync(ctx, ReportFrames.ReportedSelf, name, ct).ConfigureAwait(false);
            return;
        }
        if (slots.All(slot => slot is not null))
        {
            await SendAsync(ctx, ReportFrames.NoFreeSlot, name, ct).ConfigureAwait(false);
            return;
        }
        if (slots.Any(slot => slot is not null && string.Equals(slot.Defendant, defendant, StringComparison.Ordinal)))
        {
            await SendAsync(ctx, ReportFrames.AlreadyReported, name, ct).ConfigureAwait(false);
            return;
        }
        var found = _room.FindNamed(defendant) is { } online
            ? new ReportedCharacter(online.Character.OwnerAccountId, online.Character.CharacterId, online.Character.Name)
            : _store.FindCharacter(defendant);
        if (found is null)
        {
            await SendAsync(ctx, ReportFrames.NoSuchCharacter, ReportFrames.U32AndName(0, defendant, _text), ct).ConfigureAwait(false);
            return;
        }
        var index = Array.FindIndex(slots, slot => slot is null);
        slots[index] = new ReportSlot { Defendant = defendant, DefendantCharUid = found.CharacterId };
        await SendAsync(ctx, ReportFrames.SlotGranted, ReportFrames.U32AndName((uint)index, defendant, _text), ct).ConfigureAwait(false);
    }

    private Task AttachChat(S2SessionContext ctx, ReadOnlyMemory<byte> payload)
    {
        if (_room.Find(ctx) is not { Reports: { } slots }) return Task.CompletedTask;
        var (slot, lines) = ReportFrames.ReadChat(payload.Span, _text);
        if (slot < slots.Length && slots[slot] is { } report) report.Chat.AddRange(lines);
        return Task.CompletedTask;
    }

    private Task Fill(S2SessionContext ctx, ReadOnlyMemory<byte> payload)
    {
        if (_room.Find(ctx) is not { Reports: { } slots }) return Task.CompletedTask;
        var form = ReportFrames.ReadForm(payload.Span, _text);
        if (form.Slot >= slots.Length || slots[form.Slot] is not { } report) return Task.CompletedTask;
        report.Title = form.Title;
        report.Description = form.Description;
        report.Kind = form.Kind;
        return Task.CompletedTask;
    }

    private async Task SubmitAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { Reports: { } slots } member || payload.Length < 2) return;
        var index = new PacketReader(payload.Span).ReadUInt16();
        if (index >= slots.Length || slots[index] is not { } report) return;
        await SendAsync(ctx, ReportFrames.Queued, ReportFrames.SlotAndName(index, report.Defendant, _text), ct).ConfigureAwait(false);
        var code = _store.Insert(new PlayerReport(
            _island,
            member.Character.CharacterId,
            report.DefendantCharUid,
            report.Title,
            report.Description,
            (byte)Math.Min(report.Kind, byte.MaxValue),
            ReportFrames.PackChatting(report.Chat)));
        slots[index] = null;
        await SendAsync(ctx, ReportFrames.Filed, ReportFrames.SlotAndName(index, report.Defendant, _text), ct).ConfigureAwait(false);
        _log($"[report] 0x{member.Character.CharacterId:X} '{member.Character.Name}' reported '{report.Defendant}': report {code}, {report.Chat.Count} chat lines");
    }

    private async Task CancelAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { Reports: { } slots }) return;
        var (slot, name) = ReportFrames.ReadSlotAndName(payload.Span, _text);
        if (slot < slots.Length && slots[slot] is not null)
        {
            slots[slot] = null;
            await SendAsync(ctx, ReportFrames.Cancelled, ReportFrames.SlotAndName(slot, name, _text), ct).ConfigureAwait(false);
            return;
        }
        var match = Array.FindIndex(slots, s => s is not null && string.Equals(s.Defendant, name, StringComparison.Ordinal));
        if (match < 0)
        {
            await SendAsync(ctx, ReportFrames.NothingToCancel, ReportFrames.SlotAndName(slot, name, _text), ct).ConfigureAwait(false);
            return;
        }
        slots[match] = null;
        await SendAsync(ctx, ReportFrames.Cancelled, ReportFrames.SlotAndName((ushort)match, name, _text), ct).ConfigureAwait(false);
    }

    private async Task IsOnlineAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var name = ReportFrames.ReadName(payload.Span, _text);
        var online = _room.FindNamed(name) is not null;
        await SendAsync(ctx, ReportFrames.OnlineAnswer, ReportFrames.U32AndName(online ? 1u : 0u, name, _text), ct).ConfigureAwait(false);
    }

    private static Task SendAsync(S2SessionContext ctx, ushort opcode, byte[] payload, CancellationToken ct) =>
        ctx.SendFrameAsync(opcode, payload, (byte)Random.Shared.Next(1, 256), ct);
}
