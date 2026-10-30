using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.S2;

public enum LobbyLoginRefusal : uint
{
    None = 0,
    WrongIdOrPassword = 60003,
    AccountBlocked = 60009,
    AccountWithdrawn = 60010,
    GameBlocked = 60012,
    AlreadyLoggedIn = 60013,
}

public static class LobbyLogin
{
    public const ushort RefusalOpcode = 0x2CEF;
    public const ushort BlockNoticeOpcode = 0x2CF4;
    public const short BlockedAccess = 1;
    public const short WithdrawnAccess = 2;
    public const short BlockedGameAccess = 1;
    public const short ServerSelectedState = 3;
    public const int PenaltyNameBytes = 256;
    public const int BlockNoticeBytes = PenaltyNameBytes + 8 * sizeof(ushort);
    public static readonly TimeSpan StaleServerSelect = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan GameJoinWindow = TimeSpan.FromSeconds(30);
    public const short InGameState = 4;
    public static readonly TimeSpan PenaltyReleasePeriod = TimeSpan.FromMinutes(1);

    public static LobbyLoginRefusal ForAccess(short accountAccess) => accountAccess switch
    {
        BlockedAccess => LobbyLoginRefusal.AccountBlocked,
        WithdrawnAccess => LobbyLoginRefusal.AccountWithdrawn,
        _ => LobbyLoginRefusal.None,
    };

    public static LobbyLoginRefusal Decide(LobbyAccountState account, PenaltyNotice? runningPenalty, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(account);
        var access = ForAccess(account.AccountAccess);
        if (access != LobbyLoginRefusal.None)
        {
            return access;
        }

        if (account.GameAccess == BlockedGameAccess && runningPenalty is not null)
        {
            return LobbyLoginRefusal.GameBlocked;
        }

        if (account.State <= 0)
        {
            return LobbyLoginRefusal.None;
        }

        var staleServerSelect = account.State == ServerSelectedState
            && account.LatestLoginAt is { } loginAt
            && now - loginAt > StaleServerSelect;
        var abandonedGameJoin = account.State == InGameState
            && account.PendingJoinSince is { } handedOffAt
            && now - handedOffAt > GameJoinWindow;
        return staleServerSelect || abandonedGameJoin ? LobbyLoginRefusal.None : LobbyLoginRefusal.AlreadyLoggedIn;
    }

    public static byte[] RefusalPayload(LobbyLoginRefusal refusal)
    {
        var payload = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)refusal);
        return payload;
    }

    public static byte[] BlockNoticePayload(PenaltyNotice penalty, TimeZoneInfo serverZone, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(penalty);
        ArgumentNullException.ThrowIfNull(serverZone);
        ArgumentNullException.ThrowIfNull(text);
        var payload = new byte[BlockNoticeBytes];
        var name = text.GetBytes(penalty.Name);
        name.AsSpan(0, Math.Min(name.Length, PenaltyNameBytes - 1)).CopyTo(payload);
        var release = TimeZoneInfo.ConvertTime(penalty.ReleaseAt, serverZone);
        var fields = new[]
        {
            (ushort)release.Year,
            (ushort)release.Month,
            (ushort)0,
            (ushort)release.Day,
            (ushort)release.Hour,
            (ushort)release.Minute,
            (ushort)release.Second,
            (ushort)0,
        };
        for (var i = 0; i < fields.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(PenaltyNameBytes + i * sizeof(ushort)), fields[i]);
        }

        return payload;
    }
}
