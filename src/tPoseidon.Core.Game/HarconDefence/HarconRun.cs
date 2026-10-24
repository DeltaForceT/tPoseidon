using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.HarconDefence;

public readonly record struct HarconSpawnOrder(int MonsterId, int RegionId, int Count);

public readonly record struct HarconPlacing(ulong CharacterId, int Score, int Rank, int Coins);

public sealed class HarconRun
{
    public const int NormalWave = 0;

    private readonly IReadOnlyList<HarconWave> _waves;
    private readonly int[] _spawned = new int[HarconDefTables.SpawnGroups];
    private readonly DateTimeOffset[] _nextSpawn = new DateTimeOffset[HarconDefTables.SpawnGroups];
    private readonly Dictionary<ulong, int> _score = new();
    private readonly Dictionary<ulong, int> _waveKills = new();
    private readonly HashSet<ulong> _died = new();
    private readonly HarconPointRule _points;
    private DateTimeOffset _waveStarted;

    public HarconRun(IReadOnlyList<HarconWave> waves, HarconPointRule points, IEnumerable<ulong> members)
    {
        _waves = waves ?? throw new ArgumentNullException(nameof(waves));
        _points = points ?? throw new ArgumentNullException(nameof(points));
        ArgumentNullException.ThrowIfNull(members);
        foreach (var member in members) _score[member] = 0;
    }

    public int WaveIndex { get; private set; } = -1;
    public bool Started => WaveIndex >= 0;
    public bool LastWaveOver { get; private set; }
    public HarconWave? Wave => WaveIndex >= 0 && WaveIndex < _waves.Count ? _waves[WaveIndex] : null;
    public IReadOnlyDictionary<ulong, int> Scores => _score;

    public HarconWave? Start(DateTimeOffset now) => Started ? null : Advance(now);

    public IReadOnlyList<HarconSpawnOrder> DueSpawns(DateTimeOffset now)
    {
        if (Wave is not { } wave || LastWaveOver) return Array.Empty<HarconSpawnOrder>();
        var orders = new List<HarconSpawnOrder>();
        for (var index = 0; index < wave.Groups.Count; index++)
        {
            var group = wave.Groups[index];
            var lump = Math.Max(1, group.Lump);
            while (_spawned[index] < group.Total && now >= _nextSpawn[index])
            {
                var count = Math.Min(lump, group.Total - _spawned[index]);
                _spawned[index] += count;
                orders.Add(new HarconSpawnOrder(group.MonsterId, group.RegionId, count));
                _nextSpawn[index] = _nextSpawn[index].AddMilliseconds(group.DelayMs);
                if (group.DelayMs > 0) _nextSpawn[index] = _nextSpawn[index] < now ? now.AddMilliseconds(group.DelayMs) : _nextSpawn[index];
            }
        }
        return orders;
    }

    public bool FullySpawned => Wave is { } wave && wave.Groups.Select((group, index) => _spawned[index] >= group.Total).All(done => done);

    public HarconWave? NextWaveIfDone(DateTimeOffset now, int alive)
    {
        if (Wave is not { } wave || LastWaveOver) return null;
        var cleared = FullySpawned && alive == 0;
        var timedOut = wave.Type != NormalWave && wave.TimeSeconds > 0 && now >= _waveStarted.AddSeconds(wave.TimeSeconds);
        if (!cleared && !timedOut) return null;
        AwardWaveAce();
        if (WaveIndex + 1 >= _waves.Count)
        {
            LastWaveOver = true;
            return null;
        }
        return Advance(now);
    }

    public bool Won(int alive) => LastWaveOver && alive == 0;

    public int Killed(ulong characterId)
    {
        if (!_score.ContainsKey(characterId)) return 0;
        _score[characterId] += _points.OneOnKill;
        _waveKills[characterId] = _waveKills.GetValueOrDefault(characterId) + 1;
        return _points.OneOnKill;
    }

    public static float Contribution(int score, IEnumerable<HarconPlacing> placings)
    {
        ArgumentNullException.ThrowIfNull(placings);
        var total = placings.Sum(placing => (long)Math.Max(0, placing.Score));
        return total > 0 ? (float)(Math.Max(0, score) * 100.0 / total) : 0f;
    }

    public void Died(ulong characterId)
    {
        if (!_score.ContainsKey(characterId) || !_died.Add(characterId)) return;
        _score[characterId] += _points.Death;
    }

    public void Left(ulong characterId) => _score.Remove(characterId);

    public IReadOnlyList<HarconPlacing> Finish(bool won, HarconDropEntry? drop)
    {
        foreach (var member in _score.Keys.ToArray())
        {
            if (!_died.Contains(member)) _score[member] += _points.NoDeath;
        }
        var ordered = _score.OrderByDescending(pair => pair.Value).ToArray();
        var placings = new List<HarconPlacing>(ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var (member, score) = ordered[index];
            var rank = index + 1;
            placings.Add(new HarconPlacing(member, score, rank, won ? Coins(drop, rank, score) : 0));
        }
        return placings;
    }

    public static int Coins(HarconDropEntry? drop, int rank, int score)
    {
        if (drop is null || score < drop.MinScore) return 0;
        return rank >= 1 && rank <= drop.RankCoins.Count ? drop.RankCoins[rank - 1] : drop.Other;
    }

    private HarconWave? Advance(DateTimeOffset now)
    {
        WaveIndex++;
        _waveStarted = now;
        _waveKills.Clear();
        Array.Clear(_spawned);
        Array.Fill(_nextSpawn, now);
        return Wave;
    }

    private void AwardWaveAce()
    {
        if (_waveKills.Count == 0) return;
        var best = _waveKills.OrderByDescending(pair => pair.Value).First();
        if (_score.ContainsKey(best.Key)) _score[best.Key] += _points.WaveAce;
    }
}
