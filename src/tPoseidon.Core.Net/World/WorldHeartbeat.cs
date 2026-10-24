using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;

namespace tPoseidon.Core.Net.World;

public sealed class WorldHeartbeat : IAsyncDisposable
{
    private readonly ZoneManager _zoneManager;
    private readonly TimerNpcManager? _timerNpcs;
    private readonly AggroDetector? _aggroDetector;
    private readonly IZoneAggroScanner? _aggroScanner;
    private readonly Action<string>? _log;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public TimeSpan ZoneTickInterval { get; init; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan TimerNpcInterval { get; init; } = TimeSpan.FromMinutes(1);

    public WorldHeartbeat(
        ZoneManager zoneManager,
        TimerNpcManager? timerNpcs = null,
        AggroDetector? aggroDetector = null,
        IZoneAggroScanner? aggroScanner = null,
        Action<string>? log = null)
    {
        _zoneManager = zoneManager ?? throw new ArgumentNullException(nameof(zoneManager));
        _timerNpcs = timerNpcs;
        _aggroDetector = aggroDetector;
        _aggroScanner = aggroScanner;
        _log = log;
    }

    public void Start()
    {
        if (_loop is not null) return;
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        var nextZoneTick = DateTimeOffset.UtcNow + ZoneTickInterval;
        var nextTimerNpcTick = DateTimeOffset.UtcNow + TimerNpcInterval;
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;
                if (now >= nextZoneTick)
                {
                    nextZoneTick = now + ZoneTickInterval;
                    foreach (var zone in _zoneManager.All)
                    {
                        TickZone(zone, now);
                    }
                }
                if (_timerNpcs is not null && now >= nextTimerNpcTick)
                {
                    nextTimerNpcTick = now + TimerNpcInterval;
                    _timerNpcs.EvaluateNow(now.UtcDateTime);
                }
                await Task.Delay(ZoneTickInterval, _cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log?.Invoke($"WorldHeartbeat: loop crashed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void TickZone(Zone zone, DateTimeOffset now)
    {
        if (zone.MonsterMgr is null) return;
        if (_aggroDetector is null || _aggroScanner is null) return;

        foreach (var mob in zone.MonsterMgr.All)
        {
            if (mob.IsDead) continue;
            if (mob.Atype != MonsterAType.AggressiveMonster) continue;
            var strategy = zone.MonsterMgr.Global.GetStrategy(mob.EntityId);
            if (strategy is null) continue;
            try
            {
                _aggroDetector.Tick(mob, strategy, _aggroScanner, now);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"WorldHeartbeat: aggro tick failed for mob {mob.EntityId}: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }
        _cts.Dispose();
    }
}
