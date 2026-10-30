using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record ShadowResultInsert(ulong CharacterId, int RewardId, DateTimeOffset ExpiresAt);

public sealed record ShadowAreaReward(int Group, int TimerId, int Region, int RewardId, DateTimeOffset ExpiresAt);

public static class ShadowMessages
{
    public const int ResultInsertSize = 20;
    public const int AreaRewardSize = 24;

    public static byte[] EncodeResultInsert(ShadowResultInsert insert)
    {
        ArgumentNullException.ThrowIfNull(insert);
        var w = new PacketWriter(ResultInsertSize);
        w.WriteUInt64(insert.CharacterId);
        w.WriteInt32(insert.RewardId);
        w.WriteUInt64((ulong)insert.ExpiresAt.ToUnixTimeSeconds());
        return w.ToArray();
    }

    public static ShadowResultInsert? DecodeResultInsert(ReadOnlySpan<byte> body)
    {
        if (body.Length != ResultInsertSize) return null;
        var r = new PacketReader(body);
        var character = r.ReadUInt64();
        var reward = r.ReadInt32();
        return new ShadowResultInsert(character, reward, DateTimeOffset.FromUnixTimeSeconds((long)r.ReadUInt64()));
    }

    public static byte[] EncodeAreaReward(ShadowAreaReward reward)
    {
        ArgumentNullException.ThrowIfNull(reward);
        var w = new PacketWriter(AreaRewardSize);
        w.WriteInt32(reward.Group);
        w.WriteInt32(reward.TimerId);
        w.WriteInt32(reward.Region);
        w.WriteInt32(reward.RewardId);
        w.WriteUInt64((ulong)reward.ExpiresAt.ToUnixTimeSeconds());
        return w.ToArray();
    }

    public static ShadowAreaReward? DecodeAreaReward(ReadOnlySpan<byte> body)
    {
        if (body.Length != AreaRewardSize) return null;
        var r = new PacketReader(body);
        var group = r.ReadInt32();
        var timer = r.ReadInt32();
        var region = r.ReadInt32();
        var reward = r.ReadInt32();
        return new ShadowAreaReward(group, timer, region, reward, DateTimeOffset.FromUnixTimeSeconds((long)r.ReadUInt64()));
    }

    public static bool StoreResult(IShadowBonusStore store, InterServerFrame frame)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(frame);
        if (DecodeResultInsert(frame.Body) is not { } insert) return false;
        store.Save(new ShadowBonusRecord(insert.CharacterId, insert.RewardId, insert.ExpiresAt));
        return true;
    }

    public static IReadOnlyList<InterServerFrame> Relay(InterServerFrame frame, IEnumerable<ushort> targets)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(targets);
        return targets.Select(target => new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdShadowAreaRewardRelay,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = target,
                CharUid = 0,
                RequestId = 0,
            },
            Body = frame.Body,
        }).ToList();
    }
}
