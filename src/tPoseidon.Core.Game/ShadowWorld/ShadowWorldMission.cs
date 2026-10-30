using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.ShadowWorld;

public readonly record struct ShadowParticipant(ulong CharacterId, string Name, string Guild);

public enum ShadowOutcome
{
    None = 0,
    Player = 1,
    Guild = 2,
}

public readonly record struct ShadowResult(bool Success, ShadowOutcome Outcome, ulong WinnerCharacterId, string WinnerName);

public readonly record struct ShadowAward(ulong CharacterId, ShadowRewardEntry Reward, DateTimeOffset Expires);

public enum ShadowStandingKind : byte
{
    Empty = 0,
    Player = 1,
    Guild = 2,
}

public readonly record struct ShadowStanding(ShadowStandingKind Kind, int Score, string Name);

public sealed class ShadowWorldMission
{
    public const int TimeLimitMilliseconds = 1_200_000;
    public const int BoardIntervalMilliseconds = 5000;
    public const int SuccessLingerMilliseconds = 30_000;
    public const int FailureLingerMilliseconds = 9000;
    public const int ReadyAgainMilliseconds = 2000;
    public const int StandingSlots = 6;
    public const int BossKillBonus = 100;
    public const int IndividualRank = 1;
    public const int ParticipationRank = 2;
    public const int AreaRank = 3;
    public static readonly TimeSpan AwardLifetime = TimeSpan.FromHours(3);

    private readonly Dictionary<ulong, int> _playerScore = new();
    private readonly Dictionary<string, int> _guildScore = new(StringComparer.Ordinal);
    private readonly ShadowStanding[] _standings = new ShadowStanding[StandingSlots];
    private ulong? _bossKiller;
    private string _bossKillerGuild = string.Empty;
    private int _boardAccumulated;

    public bool Active { get; private set; }
    public bool Finished { get; private set; }
    public int ElapsedMilliseconds { get; private set; }
    public int LingerMilliseconds { get; private set; }
    public int LingeredMilliseconds { get; private set; }
    public bool ResultPending { get; private set; }
    public bool PendingSuccess { get; private set; }
    public int RemainingMilliseconds => TimeLimitMilliseconds - ElapsedMilliseconds;
    public IReadOnlyList<ShadowStanding> Standings => _standings;
    public bool LingerOver => Finished && LingeredMilliseconds > LingerMilliseconds;
    public bool ReadyAgain => Finished && LingeredMilliseconds >= LingerMilliseconds + ReadyAgainMilliseconds;

    public void Activate()
    {
        Active = true;
        ResultPending = false;
        PendingSuccess = false;
        ElapsedMilliseconds = 0;
        _boardAccumulated = 0;
        _bossKiller = null;
        _bossKillerGuild = string.Empty;
        _playerScore.Clear();
        _guildScore.Clear();
        Array.Clear(_standings);
    }

    public void CreditKill(ShadowParticipant killer)
    {
        if (!Counting) return;
        _playerScore[killer.CharacterId] = _playerScore.GetValueOrDefault(killer.CharacterId) + 1;
        if (killer.Guild.Length != 0)
        {
            _guildScore[killer.Guild] = _guildScore.GetValueOrDefault(killer.Guild) + 1;
            Rank(ShadowStandingKind.Guild, _guildScore[killer.Guild], killer.Guild);
        }
        Rank(ShadowStandingKind.Player, _playerScore[killer.CharacterId], killer.Name);
    }

    public void BossKilled(ShadowParticipant killer)
    {
        if (!Counting) return;
        _bossKiller = killer.CharacterId;
        _bossKillerGuild = killer.Guild;
        ResultPending = true;
        PendingSuccess = true;
    }

    public void StatueDestroyed()
    {
        if (!Counting) return;
        ResultPending = true;
        PendingSuccess = false;
    }

    public bool Tick(int deltaMilliseconds)
    {
        if (Finished)
        {
            LingeredMilliseconds += deltaMilliseconds;
            return false;
        }
        if (!Active) return false;
        ElapsedMilliseconds += deltaMilliseconds;
        _boardAccumulated += deltaMilliseconds;
        if (_boardAccumulated < BoardIntervalMilliseconds) return false;
        _boardAccumulated = 0;
        return true;
    }

    public ShadowResult Finish(bool success, IReadOnlyCollection<ShadowParticipant> inZone)
    {
        ArgumentNullException.ThrowIfNull(inZone);
        ResultPending = false;
        var result = Decide(success, inZone);
        LingerMilliseconds = success ? SuccessLingerMilliseconds : FailureLingerMilliseconds;
        LingeredMilliseconds = 0;
        Finished = true;
        ElapsedMilliseconds = 0;
        _boardAccumulated = 0;
        return result;
    }

    public void Close() => Active = false;

    public void Reopen() => Finished = false;

    public static IReadOnlyList<ShadowAward> Awards(
        ShadowResult result,
        IReadOnlyCollection<ShadowParticipant> recipients,
        ShadowEventTables tables,
        string scenario,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(tables);
        if (!result.Success) return Array.Empty<ShadowAward>();
        var awards = new List<ShadowAward>();
        foreach (var recipient in recipients)
        {
            var rank = RankOf(result, recipient);
            if (tables.Reward(scenario, rank) is { } reward) awards.Add(new ShadowAward(recipient.CharacterId, reward, now + AwardLifetime));
        }
        return awards;
    }

    private bool Counting => Active && !Finished && !ResultPending;

    private static int RankOf(ShadowResult result, ShadowParticipant recipient) => result.Outcome switch
    {
        ShadowOutcome.Player => recipient.CharacterId == result.WinnerCharacterId ? IndividualRank : ParticipationRank,
        ShadowOutcome.Guild => string.Equals(recipient.Guild, result.WinnerName, StringComparison.Ordinal) ? IndividualRank : ParticipationRank,
        _ => ParticipationRank,
    };

    private ShadowResult Decide(bool success, IReadOnlyCollection<ShadowParticipant> inZone)
    {
        var bestPlayer = 0;
        ulong bestPlayerId = 0;
        foreach (var participant in inZone)
        {
            if (!_playerScore.TryGetValue(participant.CharacterId, out var score)) continue;
            if (_bossKiller == participant.CharacterId) score += BossKillBonus;
            if (bestPlayer < score)
            {
                bestPlayer = score;
                bestPlayerId = participant.CharacterId;
            }
        }
        var bestGuild = 0;
        var bestGuildName = string.Empty;
        foreach (var (guild, tally) in _guildScore)
        {
            var bossGuild = _bossKillerGuild.Length != 0 && string.Equals(guild, _bossKillerGuild, StringComparison.Ordinal);
            var score = bossGuild ? tally + BossKillBonus : tally;
            if (bestGuild < score)
            {
                bestGuild = score;
                bestGuildName = guild;
            }
        }
        if (bestPlayer == 0 && bestGuild == 0) return new ShadowResult(success, ShadowOutcome.None, 0, string.Empty);
        if (bestPlayer < bestGuild) return new ShadowResult(success, ShadowOutcome.Guild, 0, bestGuildName);
        var winner = inZone.FirstOrDefault(participant => participant.CharacterId == bestPlayerId);
        if (winner.CharacterId == 0) return new ShadowResult(success, ShadowOutcome.None, 0, string.Empty);
        return winner.Guild.Length != 0
            ? new ShadowResult(success, ShadowOutcome.Guild, 0, winner.Guild)
            : new ShadowResult(success, ShadowOutcome.Player, winner.CharacterId, winner.Name);
    }

    private void Rank(ShadowStandingKind kind, int score, string name)
    {
        var slot = -1;
        var existing = -1;
        for (var index = StandingSlots - 1; index >= 0; index--)
        {
            var standing = _standings[index];
            if (standing.Kind == ShadowStandingKind.Empty || standing.Score < score) slot = index;
            if (standing.Kind != ShadowStandingKind.Empty && string.Equals(standing.Name, name, StringComparison.Ordinal)) existing = index;
        }
        if (slot == -1) return;
        if (existing == -1)
        {
            ShiftDown(slot, StandingSlots - 1);
            _standings[slot] = new ShadowStanding(kind, score, name);
        }
        else if (existing == slot)
        {
            _standings[existing] = _standings[existing] with { Score = score };
        }
        else if (slot < existing)
        {
            ShiftDown(slot, existing);
            _standings[slot] = new ShadowStanding(kind, score, name);
        }
    }

    private void ShiftDown(int from, int to)
    {
        for (var index = to; index > from; index--) _standings[index] = _standings[index - 1];
    }
}
