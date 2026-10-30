using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class TimedGrantAnnouncer
{
    private readonly IItemParamLookup _catalog;
    private readonly Func<IReadOnlyDictionary<int, int>> _periods;
    private readonly string _logTag;

    public TimedGrantAnnouncer(IItemParamLookup catalog, Func<IReadOnlyDictionary<int, int>> periods, string logTag)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _periods = periods ?? throw new ArgumentNullException(nameof(periods));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public int PeriodOf(int itemId) => TimedItemPeriods.MinutesOf(_catalog.GetById(itemId), _periods());

    public async Task AnnounceAsync(InGameSessionState player, IReadOnlyList<TimedItem> granted)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(granted);
        if (granted.Count == 0) return;
        foreach (var timed in granted) player.TimedItems.Track(timed);
        await player.SendFrameAsync(Opcode.S2C_TIMED_ITEMS_REGISTERED, TimedItemFrames.Registered(granted), (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
        foreach (var timed in granted) Console.WriteLine($"{_logTag} [timed] 0x{player.PlayerEntityId:X4} item {timed.ItemIndex} uid 0x{timed.ItemUid:X} until {timed.ExpiresAt:u}");
    }

    public Task AnnounceAsync(InGameSessionState player, EquipStatusUpgradeResult stored) =>
        AnnounceAsync(player, TimedItemPeriods.Registered(stored.Items));
}
