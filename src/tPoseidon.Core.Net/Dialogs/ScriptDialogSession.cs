using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Game.Scripting.Vm;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ScriptNpcSource(string Key);

public sealed class ScriptDialogSession
{
    public const byte OpenerRowCount = byte.MaxValue;

    private readonly Func<ScriptLibrary> _library;
    private readonly IScriptSessionHost _host;
    private readonly Func<Opcode, byte[], CancellationToken, Task> _send;
    private readonly Func<NpcEntity, ushort, CancellationToken, Task<bool>> _fallback;
    private readonly Action<string> _log;
    private readonly int _budget;
    private ScriptMachine? _machine;
    private RunState? _waiting;
    private NpcEntity? _npc;
    private ushort _entityId;
    private int _menuSize;

    public ScriptDialogSession(
        Func<ScriptLibrary> library,
        IScriptSessionHost host,
        Func<Opcode, byte[], CancellationToken, Task> send,
        Func<NpcEntity, ushort, CancellationToken, Task<bool>> fallback,
        Action<string> log,
        int instructionBudget = ScriptMachine.DefaultInstructionBudget)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _budget = instructionBudget;
    }

    public bool IsEngaged => _npc is not null;

    public Routine? FindTalk(NpcEntity npc)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var library = _library();
        if (npc.Source is ScriptNpcSource source) return library.FindNpcTalk(source.Key);
        return library.FindOverride(npc.ChrType, npc.MapId, (int)npc.Spawn.X, (int)npc.Spawn.Y);
    }

    public async Task<bool> OpenAsync(NpcEntity npc, ushort entityId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(npc);
        if (FindTalk(npc) is not { } talk) return false;
        Close();
        _npc = npc;
        _entityId = entityId;
        _machine = new ScriptMachine(talk, _library().FindFunction, _budget);
        await StepAsync(machine => machine.Start(_host), opener: true, 0, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> HandlePageLinkAsync(ushort entityId, int index, CancellationToken ct)
    {
        if (!IsEngaged || entityId != _entityId || _machine is null) return false;
        switch (_waiting)
        {
            case RunState.AwaitingNext:
                await StepAsync(machine => machine.Continue(_host), opener: false, index, ct).ConfigureAwait(false);
                break;
            case RunState.AwaitingMenu when index >= 1 && index <= _menuSize:
                await StepAsync(machine => machine.Choose(_host, index), opener: false, index, ct).ConfigureAwait(false);
                break;
            default:
                _log($"script dialog: page link {index} ignored (not waiting for it)");
                break;
        }
        return true;
    }

    public async Task<bool> HandleFunctionAsync(ushort entityId, uint functionId, CancellationToken ct)
    {
        if (!IsEngaged || entityId != _entityId) return false;
        if (functionId == 1)
        {
            await _send(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        }
        else
        {
            _log($"script dialog: NPC function {functionId} ignored while a script dialog is open");
            return true;
        }
        Close();
        return true;
    }

    public void Close()
    {
        _npc = null;
        _machine = null;
        _waiting = null;
        _entityId = 0;
        _menuSize = 0;
    }

    private async Task StepAsync(Func<ScriptMachine, RunResult> advance, bool opener, int pageIndex, CancellationToken ct)
    {
        var npc = _npc;
        var machine = _machine;
        if (npc is null || machine is null) return;
        RunResult result;
        try
        {
            result = advance(machine);
            await _host.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (ScriptRuntimeException exception)
        {
            _log(exception.Message);
            await FailAsync(ct).ConfigureAwait(false);
            return;
        }
        if (_host.TakeCloseRequest())
        {
            Close();
            return;
        }
        switch (result.State)
        {
            case RunState.AwaitingNext:
            case RunState.AwaitingMenu:
            case RunState.Closed when result.Page is not null:
                await ShowAsync(npc, result, opener, pageIndex, ct).ConfigureAwait(false);
                break;
            case RunState.Fallback:
                var entityId = _entityId;
                Close();
                if (!await _fallback(npc, entityId, ct).ConfigureAwait(false))
                {
                    await _send(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                }
                break;
            default:
                await _send(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                Close();
                break;
        }
    }

    private async Task ShowAsync(NpcEntity npc, RunResult result, bool opener, int pageIndex, CancellationToken ct)
    {
        var page = result.Page!;
        if (page.Kind == PageKind.Menu && page.Options.Count > ScriptDialogMarkup.MaximumMenuOptions)
        {
            _log($"script dialog: a menu can show at most {ScriptDialogMarkup.MaximumMenuOptions} options, this one has {page.Options.Count}");
            await FailAsync(ct).ConfigureAwait(false);
            return;
        }
        var text = ScriptDialogMarkup.Render(page);
        _waiting = result.State == RunState.Closed ? null : result.State;
        _menuSize = page.Kind == PageKind.Menu ? page.Options.Count : 0;
        if (opener)
        {
            var dialog = new NpcDialogPacket
            {
                EntityId = _entityId,
                PortraitType = (ushort)npc.ChrType,
                NpcSType = OpenerRowCount,
                Text = text,
            };
            await _send(Opcode.S2C_NPC_DIALOG, dialog.EncodePayload(), ct).ConfigureAwait(false);
            return;
        }
        await _send(Opcode.S2C_NPC_DIALOG_FOLLOWUP, QuestFrames.NpcMessage(_entityId, pageIndex, text, kind: 0, questId: 0), ct).ConfigureAwait(false);
    }

    private async Task FailAsync(CancellationToken ct)
    {
        await _send(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Close();
    }
}
