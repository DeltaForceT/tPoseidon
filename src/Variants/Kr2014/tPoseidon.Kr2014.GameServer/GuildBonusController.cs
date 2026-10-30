using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Kr2014.GameServer;

internal sealed class GuildBonusController
{
    private readonly IGuildStore _guilds;
    private readonly GuildRankBonus _bonus;
    private readonly GameMasterController _slots;
    private readonly Action<string> _log;

    public GuildBonusController(IGuildStore guilds, GuildRankBonus bonus, GameMasterController slots, Action<string> log)
    {
        _guilds = guilds ?? throw new ArgumentNullException(nameof(guilds));
        _bonus = bonus ?? throw new ArgumentNullException(nameof(bonus));
        _slots = slots ?? throw new ArgumentNullException(nameof(slots));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task RefreshAsync(InGameSessionState player, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Character is not { } character) return;
        GuildBonusStanding? standing;
        try
        {
            var membership = _guilds.FindMembershipForCharacter(character.CharacterId);
            standing = _bonus.StandingOf(membership is null ? null : _guilds.FindGuild(membership.GuildCode));
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _log($"[guild] rank bonus of 0x{player.PlayerEntityId:X4} not read: {ex.Message}");
            return;
        }
        if (!GuildRankBonus.Changed(standing, player.GuildBonusRatio)) return;
        var report = GuildRankBonus.Report(standing);
        player.GuildBonusRatio = report.Ratio;
        try
        {
            await player.SendFrameAsync(Opcode.S2C_GUILD_RANK_BONUS, GuildBonusFrames.RankBonus(report), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            await _slots.SendRateSlotsAsync(player, expFirst: true, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[guild] rank bonus for 0x{player.PlayerEntityId:X4} not delivered: {ex.Message}");
            return;
        }
        _log($"[guild] exp ratio updated for 0x{player.PlayerEntityId:X4}: rank {report.Rank}, ratio {report.Ratio}");
    }

    public async Task RefreshAllAsync(IEnumerable<InGameSessionState> players, CancellationToken ct)
    {
        foreach (var player in players)
        {
            if (player.ClientReady) await RefreshAsync(player, ct).ConfigureAwait(false);
        }
    }
}
