namespace tPoseidon.Core.Game.PvP;

public readonly record struct PvpRecord(int Ladder, int Matches, int Wins, int Losses, int Disconnects)
{
    public const int InitialLadder = 1000;

    public static PvpRecord Fresh { get; } = new(InitialLadder, 0, 0, 0, 0);
}

public static class PvpLadderRules
{
    public const int Scale = 30;

    private static readonly (int Threshold, float Underdog)[] Table =
    {
        (400, BitConverter.UInt32BitsToSingle(0x3DBA5E35)),
        (350, BitConverter.UInt32BitsToSingle(0x3DF1A9FC)),
        (300, BitConverter.UInt32BitsToSingle(0x3E1A9FBE)),
        (250, BitConverter.UInt32BitsToSingle(0x3E449BA6)),
        (200, BitConverter.UInt32BitsToSingle(0x3E75C28F)),
        (150, BitConverter.UInt32BitsToSingle(0x3E981062)),
        (100, BitConverter.UInt32BitsToSingle(0x3EB851EC)),
        (50, BitConverter.UInt32BitsToSingle(0x3EDBA5E3)),
        (0, BitConverter.UInt32BitsToSingle(0x3F000000)),
    };

    public static int Bucket(int difference)
    {
        for (var index = 0; index < Table.Length; index++)
        {
            if (Table[index].Threshold < difference) return index;
        }
        return Table.Length - 1;
    }

    public static int Points(int winnerLadder, int loserLadder)
    {
        var underdog = Table[Bucket(Math.Abs(winnerLadder - loserLadder))].Underdog;
        var upset = winnerLadder < loserLadder;
        var share = upset ? 1.0 - underdog : underdog;
        return (int)Math.Ceiling((float)(share * Scale));
    }
}

public enum PvpDuelPhase
{
    Prepare,
    Battle,
    Finished,
}

public abstract record PvpDuelEvent;

public sealed record PvpDuelBattleBegan : PvpDuelEvent;

public sealed record PvpDuelPrepareLost(int NotifiedSeat, int LostSeat) : PvpDuelEvent;

public sealed record PvpDuelFinished(PvpDuelOutcome Outcome) : PvpDuelEvent;

public sealed record PvpDuelSeatOutcome(
    ulong CharacterId,
    ushort ZoneId,
    bool TimedOut,
    bool Won,
    uint Winner,
    uint ElapsedMs,
    PvpRecord Record,
    int LadderGain,
    uint Exp,
    uint ResultCode);

public sealed record PvpDuelOutcome(bool Cancelled, int? RevivedSeat, IReadOnlyList<PvpDuelSeatOutcome> Seats)
{
    public const uint WinnerNone = 2;
    public const uint CodeWin = 1;
    public const uint CodeLose = 2;
    public const uint CodeDisconnect = 4;
}

public sealed class PvpDuel
{
    public const int Seats = 2;
    public const int PrepareMs = 5000;
    public const int BattleMs = 600000;
    public const int TimeoutMinutes = 10;
    public const byte Mode = 2;
    public const float ReviveHpRatio = 0.30f;

    private readonly ulong[] _characters = new ulong[Seats];
    private readonly ushort[] _zones = new ushort[Seats];
    private readonly PvpRecord[] _records = new PvpRecord[Seats];
    private readonly bool[] _timedOut = new bool[Seats];
    private readonly bool[] _won = new bool[Seats];
    private readonly int[] _gain = new int[Seats];
    private int _prepareElapsed;
    private int _leaver = -1;
    private uint _winner = PvpDuelOutcome.WinnerNone;

    public PvpDuel(
        ulong firstCharacter, ushort firstZone, PvpRecord firstRecord,
        ulong secondCharacter, ushort secondZone, PvpRecord secondRecord)
    {
        _characters[0] = firstCharacter;
        _characters[1] = secondCharacter;
        _zones[0] = firstZone;
        _zones[1] = secondZone;
        _records[0] = firstRecord;
        _records[1] = secondRecord;
    }

    public PvpDuelPhase Phase { get; private set; } = PvpDuelPhase.Prepare;
    public int BattleElapsedMs { get; private set; }

    public ulong CharacterOf(int seat) => _characters[seat];

    public ushort ZoneOf(int seat) => _zones[seat];

    public int SeatOf(ulong characterId) => Array.IndexOf(_characters, characterId);

    public bool Involves(ulong characterId) => SeatOf(characterId) >= 0;

    public static int Other(int seat) => 1 - seat;

    public IReadOnlyList<PvpDuelEvent> Tick(int elapsedMs)
    {
        var events = new List<PvpDuelEvent>();
        switch (Phase)
        {
            case PvpDuelPhase.Prepare:
                _prepareElapsed += elapsedMs;
                if (_prepareElapsed >= PrepareMs)
                {
                    Phase = PvpDuelPhase.Battle;
                    BattleElapsedMs = 0;
                    for (var seat = 0; seat < Seats; seat++) _records[seat] = _records[seat] with { Matches = _records[seat].Matches + 1 };
                    events.Add(new PvpDuelBattleBegan());
                }
                break;
            case PvpDuelPhase.Battle:
                BattleElapsedMs += elapsedMs;
                if (BattleElapsedMs >= BattleMs)
                {
                    _timedOut[0] = true;
                    _timedOut[1] = true;
                    Decide(loser: 0);
                    events.Add(new PvpDuelFinished(Outcome(revivedSeat: null, cancelled: false)));
                }
                break;
        }
        return events;
    }

    public IReadOnlyList<PvpDuelEvent> Died(ulong characterId)
    {
        var seat = SeatOf(characterId);
        if (seat < 0) return Array.Empty<PvpDuelEvent>();
        switch (Phase)
        {
            case PvpDuelPhase.Prepare:
                return new PvpDuelEvent[] { new PvpDuelPrepareLost(Other(seat), seat) };
            case PvpDuelPhase.Battle:
                Decide(seat);
                return new PvpDuelEvent[] { new PvpDuelFinished(Outcome(revivedSeat: seat, cancelled: false)) };
            default:
                return Array.Empty<PvpDuelEvent>();
        }
    }

    public IReadOnlyList<PvpDuelEvent> Left(ulong characterId)
    {
        var seat = SeatOf(characterId);
        if (seat < 0) return Array.Empty<PvpDuelEvent>();
        switch (Phase)
        {
            case PvpDuelPhase.Prepare:
                Phase = PvpDuelPhase.Finished;
                return new PvpDuelEvent[]
                {
                    new PvpDuelPrepareLost(Other(seat), seat),
                    new PvpDuelFinished(Outcome(revivedSeat: null, cancelled: true)),
                };
            case PvpDuelPhase.Battle:
                _leaver = seat;
                _winner = PvpDuelOutcome.WinnerNone;
                _won[Other(seat)] = true;
                _records[Other(seat)] = _records[Other(seat)] with { Wins = _records[Other(seat)].Wins + 1 };
                _records[seat] = _records[seat] with { Disconnects = _records[seat].Disconnects + 1 };
                Phase = PvpDuelPhase.Finished;
                return new PvpDuelEvent[] { new PvpDuelFinished(Outcome(revivedSeat: null, cancelled: false)) };
            default:
                return Array.Empty<PvpDuelEvent>();
        }
    }

    private void Decide(int loser)
    {
        var winner = Other(loser);
        _winner = (uint)winner;
        _won[winner] = true;
        _records[winner] = _records[winner] with { Wins = _records[winner].Wins + 1 };
        _records[loser] = _records[loser] with { Losses = _records[loser].Losses + 1 };
        var points = PvpLadderRules.Points(_records[winner].Ladder, _records[loser].Ladder);
        _gain[winner] = points;
        _gain[loser] = -points;
        _records[winner] = _records[winner] with { Ladder = _records[winner].Ladder + points };
        _records[loser] = _records[loser] with { Ladder = _records[loser].Ladder - points };
        Phase = PvpDuelPhase.Finished;
    }

    private PvpDuelOutcome Outcome(int? revivedSeat, bool cancelled)
    {
        var seats = new List<PvpDuelSeatOutcome>(Seats);
        for (var seat = 0; seat < Seats; seat++)
        {
            seats.Add(new PvpDuelSeatOutcome(
                _characters[seat], _zones[seat], _timedOut[seat], _won[seat], cancelled ? PvpDuelOutcome.WinnerNone : _winner,
                (uint)BattleElapsedMs, _records[seat], _gain[seat], 0, cancelled ? 0 : ResultCode(seat)));
        }
        return new PvpDuelOutcome(cancelled, revivedSeat, seats);
    }

    private uint ResultCode(int seat)
    {
        if (_winner == (uint)seat) return PvpDuelOutcome.CodeWin;
        if (_winner == (uint)Other(seat)) return PvpDuelOutcome.CodeLose;
        return _leaver == seat ? PvpDuelOutcome.CodeDisconnect : PvpDuelOutcome.CodeWin;
    }
}
