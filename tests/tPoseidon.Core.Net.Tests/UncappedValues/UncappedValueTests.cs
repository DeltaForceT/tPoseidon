using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.UncappedValues;

public sealed class UncappedValueTests
{
    private const long AboveUInt16 = 70_000;
    private const long AboveInt32 = 3_000_000_000;
    private const long AboveUInt32 = 6_000_000_000;

    private static readonly byte[] Int32Maximum = { 0xFF, 0xFF, 0xFF, 0x7F };

    private static Character MakeChar(long money = 0, ulong exp = 0, long hp = 41, long mp = 5, long tmExp = 0) =>
        new()
        {
            CharacterId = 0x10000001,
            OwnerAccountId = 1,
            Slot = 0,
            Name = "Tester",
            Species = Species.Lion,
            FType = FType.Power,
            Money = money,
            Exp = exp,
            CurrentHp = hp,
            CurrentMp = mp,
            Level = 1,
            TmExp = tmExp,
        };

    private static byte[] Tail(byte[] payload, int offset, int count) => payload.Skip(offset).Take(count).ToArray();

    private static PlayerStrike StrikeOf(long damage, long hpAfter) =>
        new(0x1111, 0x2222, 100, 110, 104, 112, damage, hpAfter, 0, 1, hpAfter == 0, DateTimeOffset.UnixEpoch, 77, AboveUInt32, hpAfter + damage);

    [Theory]
    [InlineData(AboveUInt16)]
    [InlineData(AboveInt32)]
    [InlineData(AboveUInt32)]
    public void DamageCalculatorAppliesTheFullValueByDefault(long attackPower)
    {
        var calc = new DamageCalculator(new ServerRates());
        var attacker = new AttackerStats(Ap: attackPower, Ma: 0, Ac: 0, GunAp: 0);
        var defender = new DefenderStats(0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));

        Assert.Equal(attackPower, result.Damage);
    }

    [Fact]
    public void DamageCalculatorHonoursAnOptionalCap()
    {
        var rates = new ServerRates { Caps = new ServerRates.CapsSection { MaxDamagePerHit = 123_456 } };
        var calc = new DamageCalculator(rates);
        var attacker = new AttackerStats(Ap: AboveUInt32, Ma: 0, Ac: 0, GunAp: 0);
        var defender = new DefenderStats(0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        Assert.Equal(123_456L, calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee)).Damage);
    }

    [Fact]
    public void MeleeFormulasDoNotWrapAboveTheInt32Range()
    {
        Assert.Equal(AboveInt32, MeleeResolution.Normal(AboveInt32, 0));
        Assert.Equal(AboveUInt32 * 3 / 2, MeleeResolution.Critical(AboveUInt32, 0));
        Assert.True(MeleeResolution.StrongCritical(AboveUInt32, 0) > AboveUInt32);
        Assert.Equal(AboveInt32 - 750, MeleeResolution.Normal(AboveInt32, 1000));
    }

    [Fact]
    public void SkillDamageDoesNotWrapAboveTheInt32Range()
    {
        var attacker = new SkillAttacker(Ap: AboveUInt32, Ac: 1000, Ma: AboveUInt32, Lk: 99, Level: 60);
        var target = new SkillTarget(Dp: 0, Hv: 0, Md: 0, Lk: 0, Hp: 10, Level: 50, Resist: 0);

        var hit = SkillDamage.Physical(attacker, AboveUInt32, target, 10f, SkillActions.NoElement, new Random(1));

        Assert.Equal(SkillDamage.PhysicalType, hit.Type);
        Assert.True(hit.Damage >= AboveUInt32, $"damage was {hit.Damage}");
    }

    [Fact]
    public void ChainSumsAddUpPastTheInt32Range()
    {
        var hits = new[] { new SkillHit(AboveInt32, 2, 0, 0, 0), new SkillHit(AboveInt32, 2, 0, 0, 0) };

        Assert.Equal(AboveInt32 * 2, new ChainVictim(1, hits).Sum);
        Assert.Equal(AboveInt32 * 2, new PolarBearOutcome(hits).Sum);
    }

    [Fact]
    public void TornadoTotalsAddUpPastTheInt32Range()
    {
        var hits = new[] { new SkillHit(AboveInt32, 2, 0, 0, 0), new SkillHit(AboveInt32, 2, 0, 0, 0), new SkillHit(AboveInt32, 2, 0, 0, 0) };

        var outcome = SkillActions.RecordTornadoHits(hits, default);

        Assert.Equal(AboveInt32 * 3, outcome.Sum);
    }

    [Fact]
    public void MonsterHealthPoolAndDamageAreBeyondInt32()
    {
        var mob = new MonsterEntity
        {
            EntityId = 1, MapId = 1, MonsterParamId = 1, Name = "Boss", SpawnX = 1, SpawnY = 1,
            MaxHp = AboveUInt32 * 2, Level = 1,
        };
        mob.InitHp(AboveUInt32 * 2);

        Assert.Equal(AboveUInt32, mob.ApplyDamage(AboveUInt32));
        Assert.Equal(AboveUInt32, mob.CurrentHp);
        Assert.False(mob.IsDead);
        Assert.Equal(AboveUInt32, mob.ApplyDamage(AboveUInt32 * 5));
        Assert.True(mob.IsDead);
    }

    [Fact]
    public void MonsterDamageLimitIsOptional()
    {
        var mob = new MonsterEntity
        {
            EntityId = 1, MapId = 1, MonsterParamId = 1, Name = "Boss", SpawnX = 1, SpawnY = 1,
            MaxHp = 10_000, Level = 1, DamageLimit = () => 250,
        };
        mob.InitHp(10_000);

        Assert.Equal(250, mob.ApplyDamage(5_000));
    }

    [Fact]
    public void PlayerVitalsMoneyAndExperienceHoldSixtyFourBitValues()
    {
        var stats = new StatManager(MakeChar(money: 100, exp: 7, hp: 41, mp: 5, tmExp: 20));

        stats.GrantMoney(AboveUInt32);
        stats.SetVitals(AboveUInt32 * 2, AboveUInt32);
        var left = stats.TakeHit(AboveUInt32);
        stats.GrantExp(AboveUInt32);
        stats.GrantTmExp(AboveUInt32);

        Assert.Equal(AboveUInt32 + 100, stats.Money);
        Assert.Equal(AboveUInt32, left);
        Assert.Equal(AboveUInt32, stats.CurrentMp);
        Assert.Equal((ulong)AboveUInt32 + 7, stats.Exp);
        Assert.Equal(AboveUInt32 + 20, stats.TmExp);
    }

    [Fact]
    public void ExcessDamageEmptiesTheHealthPoolWithoutWrapping()
    {
        var stats = new StatManager(MakeChar(hp: 500));

        Assert.Equal(0, stats.TakeHit(AboveUInt32 * 4));
        Assert.Equal(0, stats.CurrentHp);
    }

    [Fact]
    public void WalletAndDamageCapsAreOptional()
    {
        var capped = new ServerRates.CapsSection { MaxGalder = 1_000, MaxDamagePerHit = 40 };
        var stats = new StatManager(MakeChar(money: 900, hp: 500), capsProvider: () => capped);

        stats.GrantMoney(5_000);
        stats.TakeHit(1_000);

        Assert.Equal(1_000, stats.Money);
        Assert.Equal(460, stats.CurrentHp);
        Assert.Equal(1_000, stats.WalletLimit);
        Assert.Equal(long.MaxValue, new StatManager(MakeChar()).WalletLimit);
    }

    [Fact]
    public void ExperienceChartAdvancesOnHugeGains()
    {
        var chart = LevelExpChart.Parse(string.Join(' ', Enumerable.Range(1, LevelExpChart.LastLevel).Select(level => $"{level} 1 1000 1")));
        var stats = new StatManager(MakeChar());

        var gain = stats.AddExp(AboveUInt32, chart);

        Assert.Equal(ExpGainKind.LevelUp, gain.Kind);
        Assert.Equal(LevelExpChart.LastLevel - 1, gain.LevelsGained);
        Assert.Equal(LevelExpChart.LastLevel, stats.Level);
    }

    [Fact]
    public void RatesScaleBeyondInt32()
    {
        var rates = new ServerRates { Global = new ServerRates.GlobalSection { ExpRate = 4, TmxpRate = 4, GalderRate = 4 } };

        Assert.Equal(AboveInt32 * 4, rates.ScaleExp(AboveInt32));
        Assert.Equal(AboveInt32 * 4, rates.ScaleTmxp(AboveInt32));
        Assert.Equal(AboveInt32 * 4, rates.ScaleGalder(AboveInt32));
        Assert.True(GameMasterRates.Scale(AboveInt32, 50) > AboveInt32 + AboveInt32 / 2);
    }

    [Fact]
    public void WireLimitsSaturateAndNeverWrap()
    {
        Assert.Equal(int.MaxValue, WireLimits.Int32Field(AboveUInt32));
        Assert.Equal(0, WireLimits.Int32Field(-5));
        Assert.Equal(int.MaxValue, WireLimits.Int32Field(double.PositiveInfinity));
        Assert.Equal(int.MinValue, WireLimits.SaturateInt32(-AboveUInt32));
        Assert.Equal(ushort.MaxValue, WireLimits.UInt16Field(AboveUInt16 * 10));
        Assert.Equal(short.MaxValue, WireLimits.Int16Field(AboveUInt16));
        Assert.Equal(long.MaxValue, WireLimits.Saturate(1e30));
        Assert.Equal(0L, WireLimits.Saturate(double.NaN));
    }

    [Theory]
    [InlineData(AboveInt32)]
    [InlineData(AboveUInt32)]
    public void HitResultFrameSaturatesAtTheLastValueTheClientReads(long damage)
    {
        var frames = MeleeSwingFrames.Encode(StrikeOf(damage, 0), 5);

        var attack = frames.First(frame => frame.Opcode == Opcode.S2C_OP_001F_TBD).Payload;
        var hit = frames.First(frame => frame.Opcode == Opcode.S2C_OP_002A_TBD).Payload;
        Assert.Equal(new byte[] { 0xFF, 0x7F }, Tail(attack, 14, 2));
        Assert.Equal(Int32Maximum, Tail(hit, 2, 4));
    }

    [Fact]
    public void HitFramesKeepSmallValuesExactly()
    {
        var frames = MeleeSwingFrames.Encode(StrikeOf(AboveUInt16, 12345), 5);

        var attack = frames.First(frame => frame.Opcode == Opcode.S2C_OP_001F_TBD).Payload;
        var hit = frames.First(frame => frame.Opcode == Opcode.S2C_OP_002A_TBD).Payload;
        var monsterHp = frames.First(frame => frame.Opcode == Opcode.S2C_OP_011E_TBD).Payload;
        Assert.Equal(new byte[] { 0xFF, 0x7F }, Tail(attack, 14, 2));
        Assert.Equal(BitConverter.GetBytes((int)AboveUInt16), Tail(hit, 2, 4));
        Assert.Equal(BitConverter.GetBytes(12345), Tail(monsterHp, 2, 4));
    }

    [Fact]
    public void MonsterBattleFramesSaturateTheDamage()
    {
        var swung = new MonsterBattleSwung(0x3333, 50, 60, 0x4444, 52, 61, AboveUInt32, 3, true);

        var frames = MonsterBattleFrames.Encode(swung, 8);

        Assert.Contains(frames, frame => frame.Payload.Length == MeleeSwingFrames.HitResultSize && Tail(frame.Payload, 2, 4).SequenceEqual(Int32Maximum));
    }

    [Fact]
    public void MonsterHealthFrameSaturates()
    {
        Assert.Equal(Int32Maximum, Tail(SkillFrames.MonsterHpUpdate(0x2222, AboveUInt32), 2, 4));
        Assert.Equal(Int32Maximum, Tail(MeleeSwingFrames.Encode(StrikeOf(1, AboveUInt32), 5).First(frame => frame.Opcode == Opcode.S2C_OP_011E_TBD).Payload, 2, 4));
    }

    [Theory]
    [InlineData(2_147_483_647L, 2_147_483_647L)]
    [InlineData(2_147_483_648L, 2_147_483_647L)]
    [InlineData(AboveUInt32, 2_147_483_647L)]
    public void ValueFramesSaturateAtInt32Maximum(long value, long expected)
    {
        var bytes = BitConverter.GetBytes((int)expected);

        Assert.Equal(bytes, GroundItemFrames.Money(value));
        Assert.Equal(bytes, ProgressFrames.ExpGained(value));
        Assert.Equal(bytes, Tail(ProgressFrames.TmTotal(0, value), 1, 4));
        Assert.Equal(bytes, HealthStatusPacket.Of(value).EncodePayload());
        Assert.Equal(bytes, ManaStatusPacket.Of(value).EncodePayload());
        Assert.Equal(bytes, Tail(BankFrames.Money(value, 0), 0, 4));
        Assert.Equal(bytes, Tail(BankFrames.Money(0, value), 4, 4));
        Assert.Equal(bytes, Tail(ProgressFrames.TmLevelUp(1, 5, value, 1), 3, 4));
        Assert.Equal(bytes, PlayerDeathFrames.ExpValue((ulong)value));
    }

    [Fact]
    public void LevelUpFrameSaturatesHealthManaAndExperience()
    {
        var block = new CharacterStatBlock(new short[12], 0, 0, 1, 2, CharacterStatBlock.LoginMarkWhenUnset);

        var payload = ProgressFrames.LevelUp(block, AboveUInt32, AboveUInt32 * 2, (ulong)AboveUInt32 * 3);

        Assert.Equal(Int32Maximum, Tail(payload, CharacterStatBlock.Size, 4));
        Assert.Equal(Int32Maximum, Tail(payload, CharacterStatBlock.Size + 4, 4));
        Assert.Equal(Int32Maximum, Tail(payload, CharacterStatBlock.Size + 8, 4));
    }

    [Fact]
    public void QuestResultFrameSaturatesRewards()
    {
        var payload = QuestResultFrames.Result(Array.Empty<BagOperation>(), Array.Empty<(uint, ushort)>(), AboveUInt32, AboveUInt32, 12, 3, AboveUInt32);

        var exp = Tail(payload, payload.Length - 18, 4);
        Assert.Equal(Int32Maximum, exp);
    }

    [Fact]
    public void CharacterSnapshotSurvivesTheInterServerRoundTripAboveUInt32()
    {
        var character = MakeChar(money: AboveUInt32, hp: AboveUInt32 * 2, mp: AboveUInt32 + 1, tmExp: AboveUInt32 + 2);

        var decoded = tPoseidon.Core.InterServer.CharStoreWire.DecodeCharacter(tPoseidon.Core.InterServer.CharStoreWire.EncodeCharacter(character));

        Assert.Equal(AboveUInt32, decoded.Money);
        Assert.Equal(AboveUInt32 * 2, decoded.CurrentHp);
        Assert.Equal(AboveUInt32 + 1, decoded.CurrentMp);
        Assert.Equal(AboveUInt32 + 2, decoded.TmExp);
    }

    [Fact]
    public void LoginRecordCarriesTheLastValueTheClientReads()
    {
        var info = new tPoseidon.Kr2014.Wire.CharInfoPacket
        {
            Stats = new CharacterStatBlock(new short[12], 0, 0, 1, 2, CharacterStatBlock.LoginMarkAsStored),
            Money = WireLimits.Int32FieldUnsigned(AboveUInt32),
            Hp = WireLimits.Int32Field(AboveUInt32),
            Mp = WireLimits.Int32Field(AboveUInt32),
            TmExp = WireLimits.Int32FieldUnsigned(AboveUInt32),
            Exp = WireLimits.Int32FieldUnsigned((ulong)AboveUInt32),
        };

        var decoded = tPoseidon.Kr2014.Wire.CharInfoPacket.DecodePayload(info.EncodePayload());

        Assert.Equal((uint)int.MaxValue, decoded.Money);
        Assert.Equal(int.MaxValue, decoded.Hp);
        Assert.Equal(int.MaxValue, decoded.Mp);
        Assert.Equal((uint)int.MaxValue, decoded.TmExp);
        Assert.Equal((uint)int.MaxValue, decoded.Exp);
    }
}
