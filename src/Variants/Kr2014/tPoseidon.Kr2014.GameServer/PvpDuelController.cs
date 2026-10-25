using System.Collections.Concurrent;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PvP;
using tPoseidon.Core.Net.World;

internal sealed class PvpDuelController : IDisposable
{
    private sealed class ActiveDuel
    {
        public ActiveDuel(PvpDuel duel, byte[] previousModes)
        {
            Duel = duel;
            PreviousModes = previousModes;
        }

        public PvpDuel Duel { get; }
        public byte[] PreviousModes { get; }
        public DateTimeOffset BattleBegan { get; set; }
    }

    private readonly IPvpStore _store;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<InGameSessionState, Task> _reviveLoser;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly PvpDuelRequests _requests = new();
    private readonly ConcurrentDictionary<ulong, ActiveDuel> _byCharacter = new();
    private readonly List<ActiveDuel> _duels = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Random _rng = new();
    private DateTimeOffset _lastTick;

    public PvpDuelController(
        IPvpStore store, Func<IReadOnlyCollection<InGameSessionState>> sessions, Func<InGameSessionState, Task> reviveLoser, Encoding text, string logTag)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _reviveLoser = reviveLoser ?? throw new ArgumentNullException(nameof(reviveLoser));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public void Dispose() => _sync.Dispose();

    public bool MayStrike(InGameSessionState attacker, InGameSessionState victim)
    {
        var attackerDuel = DuelOf(attacker);
        var victimDuel = DuelOf(victim);
        if (attackerDuel is null && victimDuel is null) return true;
        return ReferenceEquals(attackerDuel, victimDuel);
    }

    public Task RequestAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } requester || !PvpDuelFrames.TryReadName(payload.Span, _text, out var targetName)) return;
        if (_byCharacter.ContainsKey(requester.CharacterId)) return;
        var target = SessionNamed(targetName);
        if (target?.Character is not { } targetCharacter)
        {
            await SendAsync(st, Opcode.S2C_PVP_DUEL_NO_REQUEST_TARGET, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (ReferenceEquals(target, st) || target.MapId != st.MapId) return;
        if (_byCharacter.ContainsKey(targetCharacter.CharacterId))
        {
            await SendAsync(st, Opcode.PVP_DUEL_ANSWER, PvpDuelFrames.Answer(PvpDuelFrames.AnswerBusy, null)).ConfigureAwait(false);
            return;
        }
        _requests.Ask(requester.CharacterId, targetCharacter.CharacterId);
        await SendAsync(target, Opcode.PVP_DUEL_REQUEST, ParticipantOf(st)).ConfigureAwait(false);
        Log($"0x{st.PlayerEntityId:X4} challenges 0x{target.PlayerEntityId:X4}");
    });

    public Task AnswerAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } responder || !PvpDuelFrames.TryReadNamedCode(payload.Span, _text, out var requesterName, out var answer)) return;
        var requester = SessionNamed(requesterName);
        if (requester?.Character is not { } requesterCharacter)
        {
            await SendAsync(st, Opcode.S2C_PVP_DUEL_NO_ANSWER_TARGET, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (requester.MapId != st.MapId) return;
        var accepted = answer == PvpDuelFrames.AnswerAccepted;
        if (!_requests.Answer(responder.CharacterId, requesterCharacter.CharacterId, accepted)) return;
        await SendAsync(requester, Opcode.PVP_DUEL_ANSWER, PvpDuelFrames.Answer(answer, accepted ? ParticipantOf(st) : null)).ConfigureAwait(false);
        Log($"0x{st.PlayerEntityId:X4} answers 0x{requester.PlayerEntityId:X4} with {answer}");
    });

    public Task ControlAsync(InGameSessionState st, ReadOnlyMemory<byte> payload) => Locked(async () =>
    {
        if (st.Character is not { } sender || !PvpDuelFrames.TryReadNamedCode(payload.Span, _text, out var targetName, out var code)) return;
        var target = SessionNamed(targetName);
        if (target?.Character is not { } targetCharacter)
        {
            await SendAsync(st, Opcode.S2C_PVP_DUEL_NO_START_TARGET, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        switch (code)
        {
            case PvpDuelFrames.ControlStart:
                await StartAsync(st, sender, target, targetCharacter).ConfigureAwait(false);
                break;
            case PvpDuelFrames.ControlCancel:
                _requests.Cancel(sender.CharacterId, targetCharacter.CharacterId);
                await SendAsync(target, Opcode.PVP_DUEL_CONTROL, PvpDuelFrames.Cancelled()).ConfigureAwait(false);
                break;
        }
    });

    public Task PlayerDiedAsync(InGameSessionState victim) => Locked(async () =>
    {
        if (victim.Character is not { } character || !_byCharacter.TryGetValue(character.CharacterId, out var active)) return;
        await ApplyAsync(active, active.Duel.Died(character.CharacterId), DateTimeOffset.UtcNow).ConfigureAwait(false);
    });

    public Task LeftAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character) return;
        _requests.Forget(character.CharacterId);
        if (!_byCharacter.TryGetValue(character.CharacterId, out var active)) return;
        await ApplyAsync(active, active.Duel.Left(character.CharacterId), DateTimeOffset.UtcNow).ConfigureAwait(false);
    });

    public Task EnteredZoneAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || !_byCharacter.TryGetValue(character.CharacterId, out var active)) return;
        if (st.MapId == active.Duel.ZoneOf(active.Duel.SeatOf(character.CharacterId))) return;
        await ApplyAsync(active, active.Duel.Left(character.CharacterId), DateTimeOffset.UtcNow).ConfigureAwait(false);
    });

    public Task TickAsync(DateTimeOffset now) => Locked(async () =>
    {
        var elapsed = _lastTick == default ? 0 : (int)Math.Clamp((now - _lastTick).TotalMilliseconds, 0, int.MaxValue);
        _lastTick = now;
        foreach (var active in _duels.ToArray()) await ApplyAsync(active, active.Duel.Tick(elapsed), now).ConfigureAwait(false);
    });

    private async Task StartAsync(InGameSessionState starter, Character sender, InGameSessionState target, Character targetCharacter)
    {
        if (!_requests.Start(sender.CharacterId, targetCharacter.CharacterId)) return;
        if (_byCharacter.ContainsKey(sender.CharacterId) || _byCharacter.ContainsKey(targetCharacter.CharacterId)) return;
        if (starter.MapId != target.MapId || starter.IsDead || target.IsDead) return;
        var duel = new PvpDuel(
            sender.CharacterId, starter.MapId, RecordOf(sender.CharacterId),
            targetCharacter.CharacterId, target.MapId, RecordOf(targetCharacter.CharacterId));
        var active = new ActiveDuel(duel, new[] { starter.PvpMode, target.PvpMode });
        _duels.Add(active);
        _byCharacter[sender.CharacterId] = active;
        _byCharacter[targetCharacter.CharacterId] = active;
        starter.PvpMode = PvpDuel.Mode;
        target.PvpMode = PvpDuel.Mode;
        await SendAsync(starter, Opcode.S2C_PVP_DUEL_PREPARE, PvpDuelFrames.Prepare(PvpDuel.PrepareMs, targetCharacter.Name, PvpDuel.Mode, _text)).ConfigureAwait(false);
        await SendAsync(target, Opcode.S2C_PVP_DUEL_PREPARE, PvpDuelFrames.Prepare(PvpDuel.PrepareMs, sender.Name, PvpDuel.Mode, _text)).ConfigureAwait(false);
        Log($"duel between 0x{starter.PlayerEntityId:X4} and 0x{target.PlayerEntityId:X4} is preparing");
    }

    private async Task ApplyAsync(ActiveDuel active, IReadOnlyList<PvpDuelEvent> events, DateTimeOffset now)
    {
        foreach (var raised in events)
        {
            switch (raised)
            {
                case PvpDuelBattleBegan:
                    active.BattleBegan = now;
                    for (var seat = 0; seat < PvpDuel.Seats; seat++)
                    {
                        if (SessionOf(active, seat) is { } fighter) await SendAsync(fighter, Opcode.S2C_PVP_DUEL_BATTLE, PvpDuelFrames.Battle(PvpDuel.BattleMs)).ConfigureAwait(false);
                    }
                    Log($"duel of character {active.Duel.CharacterOf(0)} and {active.Duel.CharacterOf(1)} is on");
                    break;
                case PvpDuelPrepareLost lost:
                    if (SessionOf(active, lost.NotifiedSeat) is { } notified && SessionOf(active, lost.LostSeat)?.Character is { } gone)
                    {
                        await SendAsync(notified, Opcode.S2C_PVP_DUEL_CANCELLED, PvpDuelFrames.PrepareLost(gone.Name, _text)).ConfigureAwait(false);
                    }
                    break;
                case PvpDuelFinished finished:
                    await FinishAsync(active, finished.Outcome, now).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task FinishAsync(ActiveDuel active, PvpDuelOutcome outcome, DateTimeOffset now)
    {
        _duels.Remove(active);
        for (var seat = 0; seat < PvpDuel.Seats; seat++)
        {
            var characterId = active.Duel.CharacterOf(seat);
            _byCharacter.TryRemove(characterId, out _);
            _requests.Forget(characterId);
        }
        PvpDuelLedger.Save(_store, outcome, active.BattleBegan, now);
        for (var seat = 0; seat < PvpDuel.Seats; seat++)
        {
            if (SessionOf(active, seat) is not { } fighter) continue;
            fighter.PvpMode = active.PreviousModes[seat];
            if (!outcome.Cancelled) await SendAsync(fighter, Opcode.S2C_PVP_DUEL_RESULT, PvpDuelFrames.Result(outcome.Seats[seat])).ConfigureAwait(false);
            await SendAsync(fighter, Opcode.S2C_PVP_DUEL_END, PvpDuelFrames.End(fighter.PvpMode)).ConfigureAwait(false);
        }
        if (outcome.RevivedSeat is { } revived && SessionOf(active, revived) is { IsDead: true } loser) await _reviveLoser(loser).ConfigureAwait(false);
        Log($"duel of character {active.Duel.CharacterOf(0)} and {active.Duel.CharacterOf(1)} ended (cancelled {outcome.Cancelled})");
    }

    private byte[] ParticipantOf(InGameSessionState st)
    {
        var character = st.Character!;
        var record = RecordOf(character.CharacterId);
        return PvpDuelFrames.Participant(character.Name, st.Stats?.Level ?? character.Level, record, _text);
    }

    private PvpRecord RecordOf(ulong characterId)
    {
        var ladder = _store.GetOrCreateLadder(characterId);
        return new PvpRecord(ladder.Ladder, ladder.MatchCount, ladder.WinCount, ladder.LoseCount, ladder.DiscCount);
    }

    private ActiveDuel? DuelOf(InGameSessionState st) =>
        st.Character is { } character && _byCharacter.TryGetValue(character.CharacterId, out var active) ? active : null;

    private InGameSessionState? SessionOf(ActiveDuel active, int seat)
    {
        var characterId = active.Duel.CharacterOf(seat);
        return _sessions().FirstOrDefault(session => session.Character?.CharacterId == characterId);
    }

    private InGameSessionState? SessionNamed(string name) =>
        _sessions().FirstOrDefault(session => session.Character is { } character && character.Name == name);

    private void Log(string message) => Console.WriteLine($"{_logTag} [pvp duel] {message}");

    private async Task Locked(Func<Task> action)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
