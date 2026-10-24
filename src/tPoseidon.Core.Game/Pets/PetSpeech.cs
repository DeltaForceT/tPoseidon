using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Pets;

public readonly record struct PetUtterance(string Text, int Color);

public sealed class PetSpeechBook
{
    public const int RollRange = 1000;

    private readonly PetSpeechTables _tables;

    public PetSpeechBook(PetSpeechTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _tables = tables;
    }

    public static PetSpeechBook Empty { get; } = new(PetSpeechTables.Empty);

    public static int SpeechTypeOf(int eventCode) => eventCode - 1;

    public PetUtterance? Pick(int chrType, int speechType, Func<int, int> nextBelow)
    {
        ArgumentNullException.ThrowIfNull(nextBelow);
        if (!_tables.Params.TryGetValue(chrType, out var param)) return null;
        if (!_tables.Tables.TryGetValue(param.Table, out var byType)) return null;
        if (!byType.TryGetValue(speechType, out var lines) || lines.Count < 1) return null;
        if (lines.Count == 1) return new PetUtterance(lines[0].Text, param.Color);
        var roll = nextBelow(RollRange);
        foreach (var line in lines)
        {
            if (roll < line.Threshold) return new PetUtterance(line.Text, param.Color);
        }
        return null;
    }
}

public sealed class PetSpeechState
{
    public const int ShortCooldownMs = 3000;
    public const int LongCooldownMs = 6000;
    public const byte OwnerDiedEvent = 3;
    public const byte OwnerSatEvent = 0x0A;
    public const byte OwnerLayDownEvent = 0x0B;

    private static readonly int[] LongCooldownEvents = [6, 7];

    private int _pending;
    private bool _cooling;
    private int _coolingMs;
    private int _cooldownMs;
    private DateTimeOffset _tickedAt;

    public static byte? EventForPosture(byte postureCode) => postureCode switch
    {
        NaturalRecovery.SitAction => OwnerSatEvent,
        NaturalRecovery.LieAction => OwnerLayDownEvent,
        _ => null,
    };

    public void Raise(byte eventCode)
    {
        _cooldownMs = Array.IndexOf(LongCooldownEvents, (int)eventCode) >= 0 ? LongCooldownMs : ShortCooldownMs;
        if (eventCode == OwnerDiedEvent) Clear();
        if (!_cooling && _pending == 0) _pending = eventCode;
    }

    public PetUtterance? Tick(DateTimeOffset now, PetSpeechBook book, int chrType, Func<int, int> nextBelow)
    {
        ArgumentNullException.ThrowIfNull(book);
        var elapsedMs = _tickedAt == default ? 0 : (int)Math.Max(0, (now - _tickedAt).TotalMilliseconds);
        _tickedAt = now;
        if (_cooling) _coolingMs += elapsedMs;
        if (_cooldownMs < _coolingMs) Clear();
        if (_pending == 0) return null;
        if (book.Pick(chrType, PetSpeechBook.SpeechTypeOf(_pending), nextBelow) is { } spoken)
        {
            _pending = 0;
            _cooling = true;
            return spoken;
        }
        Clear();
        return null;
    }

    private void Clear()
    {
        _cooling = false;
        _coolingMs = 0;
        _pending = 0;
    }
}
