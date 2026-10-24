using System.Globalization;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.UncappedValues;

public static class NormalValueSnapshot
{
    public static IEnumerable<string> Lines()
    {
        foreach (var line in DamageCalculatorLines()) yield return line;
        foreach (var line in MeleeLines()) yield return line;
        foreach (var line in SkillDamageLines()) yield return line;
        foreach (var line in HelperLines()) yield return line;
        foreach (var line in StatManagerLines()) yield return line;
        foreach (var line in MonsterLines()) yield return line;
        foreach (var line in FrameLines()) yield return line;
    }

    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);

    private static string Join(params object[] parts) =>
        string.Join(' ', parts.Select(part => Convert.ToString(part, CultureInfo.InvariantCulture)));

    private static IEnumerable<string> DamageCalculatorLines()
    {
        var calc = new DamageCalculator(new ServerRates());
        var kinds = new[] { DamageKind.PhysicalMelee, DamageKind.PhysicalSkill, DamageKind.PowerSkill, DamageKind.MagicSkill, DamageKind.Gun, DamageKind.Poison };
        foreach (var kind in kinds)
        foreach (var ap in new[] { 1, 18, 100, 777, 2500, 9999 })
        foreach (var dp in new[] { 0, 50, 400, 1500 })
        foreach (var crit in new[] { false, true })
        foreach (var mob in new[] { false, true })
        foreach (var element in new[] { DamageElement.None, DamageElement.Fire })
        foreach (var attr in new[] { 0, 30 })
        {
            var attacker = new AttackerStats(Ap: ap, Ma: ap / 2, Ac: ap / 10 + 50, GunAp: ap / 5);
            var defender = new DefenderStats(
                Dp: dp, Md: dp / 2, Hv: dp / 10, CurrentHp: 5000, PhysResistPct: 10, MagicResistPct: 5, GunResistPct: 0,
                FireResistPct: 20, WaterResistPct: 0, AirResistPct: 0, SoilResistPct: 0, ElectricityResistPct: 0,
                LightResistPct: 0, DarkResistPct: 0, ShadowResistPct: 0);
            var result = calc.Compute(attacker, defender, new DamageInput(kind, element, SkillBaseDamage: ap * 2, AttrPercent: attr, IsCrit: crit, IsMobAttack: mob));
            yield return Join("calc", kind, ap, dp, crit, mob, element, attr, (long)result.Damage, result.HitFlags, result.HitKind);
        }
    }

    private static IEnumerable<string> MeleeLines()
    {
        for (var seed = 1; seed <= 40; seed++)
        foreach (var ap in new[] { 20, 300, 4000 })
        foreach (var dp in new[] { 0, 100, 900 })
        {
            var rng = new Random(seed);
            var player = MeleeResolution.PlayerAgainstMonster(ap, 90, 12, 0, dp, 30, 20, 8000, rng, 5, false);
            yield return Join("melee-pm", seed, ap, dp, (long)player.Damage, player.Result, player.ArmedNext);
            var armed = MeleeResolution.PlayerAgainstMonster(ap, 70, 5, MeleeResolution.ResultCritical, dp, 30, 20, 100, new Random(seed), 0, true);
            yield return Join("melee-armed", seed, ap, dp, (long)armed.Damage, armed.Result, armed.ArmedNext);
            var mobHit = MeleeResolution.MonsterAgainstPlayer(ap, 85, 10, 40, seed % 2 == 0, dp, 25, 15, 38, 6000, seed % 7 == 0, new Random(seed));
            yield return Join("melee-mp", seed, ap, dp, (long)mobHit.Damage, mobHit.Result, mobHit.ArmedNext);
        }
        foreach (var ap in new[] { 0, 1, 99, 1200, 40000 })
        foreach (var dp in new[] { 0, 10, 1000 })
        {
            yield return Join("melee-formula", ap, dp, (long)MeleeResolution.Normal(ap, dp), (long)MeleeResolution.Critical(ap, dp), (long)MeleeResolution.StrongCritical(ap, dp));
        }
    }

    private static IEnumerable<string> SkillDamageLines()
    {
        for (var seed = 1; seed <= 30; seed++)
        foreach (var power in new[] { 10, 250, 3000 })
        foreach (var dp in new[] { 0, 120, 1500 })
        {
            var attacker = new SkillAttacker(Ap: 500, Ac: 120, Ma: 400, Lk: 25, Level: 60, Da: 10, Hp: 3000, MaxHp: 6000, Hv: 30, Dx: 20, Dp: 100, WeaponDa: 5, DarkBonus: 0.2f, Weight: 400, ElementBonus: 40);
            var target = new SkillTarget(Dp: dp, Hv: 40, Md: dp / 2, Lk: 20, Hp: 2000, Level: 50, Resist: seed % 4 * 15, MaxHp: 9000, BaseDp: dp);
            var physical = SkillDamage.Physical(attacker, power, target, 1.2f, SkillActions.NoElement, new Random(seed));
            yield return Join("skill-phys", seed, power, dp, (long)physical.Damage, physical.Type, physical.Kind, physical.Element, (long)physical.Healed, physical.Flags);
            var magic = SkillDamage.Magic(attacker, power, target, 1.1f, 7, SkillActions.DarkElement, new Random(seed));
            yield return Join("skill-magic", seed, power, dp, (long)magic.Damage, magic.Type, magic.Kind, magic.Element, (long)magic.Healed, magic.Flags);
        }
        for (var seed = 1; seed <= 30; seed++)
        foreach (var ap in new[] { 40, 800, 6000 })
        {
            var pvp = tPoseidon.Core.Game.PvP.PvpHitRules.Strike(ap, 95, (byte)(seed % 5), 200, 40, 5000, seed % 9 == 0, new Random(seed));
            yield return Join("pvp", seed, ap, (long)pvp.Damage, pvp.Result, pvp.ArmedNext);
        }
        foreach (var mode in new byte[] { 0, 1, 2, 3 })
        foreach (var ap in new[] { 10, 600, 9000 })
        {
            yield return Join("pvp-damage", mode, ap, (long)tPoseidon.Core.Game.PvP.PvpHitRules.Damage(ap, 250, tPoseidon.Core.Game.PvP.PvpHitRules.DamageFactor(mode)));
        }
        foreach (var critical in new[] { false, true })
        foreach (var roll in new[] { 0, 30, 99 })
        foreach (var power in new[] { 50, 400, 2500 })
        {
            var target = new SkillTarget(Dp: 0, Hv: 12, Md: 0, Lk: 10, Hp: 900, Level: 5, Resist: 10);
            var shot = GunAttack.Shot(80, 15, power, 1.5f, critical, target, roll % 2 == 0, _ => roll);
            yield return Join("gun", critical, roll, power, (long)shot.Damage, shot.Kind);
        }
    }

    private static IEnumerable<string> HelperLines()
    {
        foreach (var damage in new[] { 1, 90, 4000, 70000 })
        foreach (var share in new[] { 0.1f, 0.5f, 0.99f })
        {
            yield return Join("helpers-a", damage, share,
                (long)ObjectHits.RatioPower(damage, share), (long)ObjectHits.GalderPower(damage, share),
                (long)ObjectHits.CardPower(damage, damage / 3, share), (long)ObjectHits.CardFalloff(damage, 3.5f, 8),
                (long)ObjectHits.CelestialPower(damage, 3), (long)ObjectHits.FlashPower(damage, 77, true),
                (long)ObjectEffects.WeightPower(damage, share), (long)ObjectEffects.PoisonTickDamage(damage, share),
                (long)ObjectEffects.PoisonAssaultPower(damage, 12, share), (long)ObjectEffects.FullHousePower(damage, 30, 20, share),
                (long)ObjectEffects.FullHouseReduced(damage, 15));
            yield return Join("helpers-b", damage, share,
                (long)MonsterSkills.DotDamage(damage, share), (long)MonsterSkills.DestructionPower(damage, share),
                (long)MonsterSkills.SirenPower(damage, 40, share), (long)MonsterSkills.RingFalloff(damage, 2.5f, 6f),
                (long)MonsterSkills.PoisonDamage(damage / 10, damage), (long)KnockSkills.Power(4, damage, share),
                (long)SkillAreaRules.WithFalloff(damage, 2.5f, 6f), (long)SupportSkills.FirstAidHeal(damage, 20, share),
                (long)SupportSkills.GraceHeal((uint)damage, damage / 4, share),
                (long)IncomingHits.SkinReduction(damage, 55, share), (long)IncomingHits.MirrorShare(damage, 55, share),
                (long)IncomingHits.BarrierShare(damage, 300, share));
            var kamikaze = ObjectHits.KamikazeBlast(damage, 0.4f, share);
            yield return Join("kamikaze", damage, share, (long)kamikaze.Lost, (long)kamikaze.VictimDamage);
        }
        foreach (var hp in new[] { 0, 1, 500, 12000 })
        foreach (var maxHp in new[] { 1, 800, 12000 })
        foreach (var ratio in new[] { 0.25f, 0.9f })
        {
            var heal = SkillActions.Heal((uint)hp, (uint)maxHp, ratio);
            var support = SupportSkills.Heal((uint)hp, (uint)maxHp, 700);
            var mana = SupportSkills.ConvertMana((uint)hp, (uint)maxHp, 40, 500, ratio, 0.5f);
            yield return Join("heal", hp, maxHp, ratio, (long)heal.Hp, heal.Healed, (long)support.Hp, support.Healed,
                mana.Applied, (long)mana.Hp, (long)mana.Mp,
                (long)ScrollSkills.Healed((uint)hp, (uint)maxHp, ratio), (long)FormSkills.HpAfterMorph((uint)hp, ratio));
        }
        foreach (var gain in new[] { 0, 1, 77, 5000, 2_000_000 })
        foreach (var percent in new ushort[] { 0, 50, 100, 300 })
        {
            yield return Join("gm-scale", gain, percent, (long)GameMasterRates.Scale(gain, percent));
        }
        foreach (var exp in new[] { 100, 5000, 90000 })
        foreach (var damage in new[] { 1, 700, 20000 })
        foreach (var maxHp in new[] { 300, 20000 })
        {
            var share = CombatExperience.Share(exp, damage, maxHp, maxHp);
            yield return Join("exp", exp, damage, maxHp, share, CombatExperience.ForHit(share, 40, 45), CombatExperience.TmForKill(exp / 10, maxHp, maxHp, 45, 40));
        }
    }

    private static Character MakeChar(uint money, ulong exp, uint hp, uint mp, ushort level, uint tmExp, ushort tmLevel, ushort tmPoint) =>
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
            Level = level,
            TmExp = tmExp,
            TmLevel = tmLevel,
            TmPoint = tmPoint,
        };

    private static IEnumerable<string> StatManagerLines()
    {
        var levels = LevelExpChart.Parse("1 50 500 500 2 68 680 1180 3 98 980 2160 4 144 1440 3600 5 200 2000 5600 6 300 3000 8600");
        var tmLevels = new TmLevelChart(new[] { (0, 100), (1, 250), (2, 600), (3, 1500) });
        foreach (var gain in new[] { 1, 499, 500, 1180, 4000, 90000 })
        {
            var sm = new StatManager(MakeChar(100, 7, 41, 5, 1, 20, 0, 0));
            var exp = sm.AddExp(gain, levels);
            var tm = sm.AddTmExp(gain, tmLevels);
            yield return Join("stat-gain", gain, exp.Kind, exp.LevelsGained, exp.Level, exp.Exp, tm.SendsTotal, (long)tm.TmExp,
                string.Join(',', tm.LevelUps.Select(step => $"{step.TmLevel}/{(long)step.TmExp}/{step.TmPoint}")), sm.Level, sm.TmLevel, sm.TmPoint);
        }
        var life = new StatManager(MakeChar(1000, 5000, 300, 80, 12, 0, 0, 0));
        life.GrantMoney(250);
        life.GrantMoney(-5000);
        var afterHit = life.TakeHit(120);
        var afterBig = life.TakeHit(9999);
        var died = life.Die();
        var loss = life.DeathExpLoss;
        life.Resurrect(500, 200, 0.5f, 0.25f, 0.5f);
        var restart = life.Restart(500);
        var restore = life.RestoreVitals(300, 500, -10, 200);
        life.SetVitals(222, 33);
        yield return Join("stat-life", life.Money, afterHit, afterBig, died, loss, restart, restore.Hp, restore.Mp, life.CurrentHp, life.CurrentMp, life.Exp, StatManager.RestartHp(777));
        var grants = new StatManager(
            MakeChar(0, 0, 41, 5, 1, 0, 0, 0),
            LevelExpTable.FromInlineThresholds(new Dictionary<int, ulong> { [1] = 100, [2] = 300 }, new Dictionary<int, ulong> { [0] = 50, [1] = 120 }));
        grants.GrantExp(450);
        grants.GrantTmExp(130);
        yield return Join("stat-grant", grants.Level, grants.Exp, grants.TmLevel, grants.TmExp, grants.TmPoint);
    }

    private static IEnumerable<string> MonsterLines()
    {
        foreach (var maxHp in new[] { 40, 5000, 800000 })
        {
            var mob = new MonsterEntity
            {
                EntityId = 0x2001,
                MapId = 5,
                MonsterParamId = 77,
                Name = "Dummy",
                SpawnX = 10,
                SpawnY = 12,
                MaxHp = maxHp,
                Level = 30,
            };
            mob.InitHp(maxHp);
            var parts = new List<string>();
            foreach (var hit in new[] { 0, -5, 1, maxHp / 3, maxHp / 3, maxHp, 7 })
            {
                var applied = mob.ApplyDamage(hit);
                parts.Add($"{(long)applied}/{(long)mob.CurrentHp}/{mob.IsDead}");
            }
            mob.InitHp(maxHp / 2);
            mob.RaiseHp(maxHp * 2);
            parts.Add($"raised/{(long)mob.CurrentHp}");
            yield return Join("mob", maxHp, string.Join(',', parts));
        }
    }

    private static IEnumerable<string> FrameLines()
    {
        foreach (var damage in new[] { 0, 1, 321, 65535, 70000 })
        foreach (var killed in new[] { false, true })
        {
            var hpAfter = killed ? 0 : 4321;
            var strike = new PlayerStrike(0x1111, 0x2222, 100, 110, 104, 112, damage, hpAfter, 0, 1, killed, DateTimeOffset.UnixEpoch, 77, 9000, 4321 + damage);
            var frames = MeleeSwingFrames.Encode(strike, 5);
            yield return Join("swing", damage, killed, string.Join('|', frames.Select(frame => $"{frame.Opcode}:{frame.BlockId}:{frame.Audience}:{Hex(frame.Payload)}")));
            var onPlayer = MeleeSwingFrames.EncodeOnPlayer(strike, 6, Array.Empty<SwingFrame>(), Array.Empty<SwingFrame>());
            yield return Join("swing-player", damage, killed, string.Join('|', onPlayer.Select(frame => $"{frame.Opcode}:{Hex(frame.Payload)}")));
            var gun = MeleeSwingFrames.EncodeGunShot(strike, 7);
            yield return Join("gun-shot", damage, killed, string.Join('|', gun.Select(frame => $"{frame.Opcode}:{Hex(frame.Payload)}")));
            var swung = new MonsterBattleSwung(0x3333, 50, 60, 0x4444, 52, 61, damage, 3, killed);
            var battle = MonsterBattleFrames.Encode(swung, 8);
            yield return Join("battle", damage, killed, string.Join('|', battle.Select(frame => $"{frame.Opcode}:{frame.BlockId}:{Hex(frame.Payload)}")));
        }
        foreach (var hp in new[] { 0, 1, 777, 65535, 123456 })
        {
            yield return Join("monster-hp", hp, Hex(SkillFrames.MonsterHpUpdate(0x2222, hp)));
            yield return Join("flag-amount", hp, Hex(SkillFrames.FlagAmountTail(true, hp)), Hex(SkillFrames.FlagAmountPointTail(false, hp, (12, 34))));
            yield return Join("shield-gauge", hp, Hex(SkillEffectFrames.ShieldGauge(0x4321, hp, hp + 100)));
            yield return Join("hp-packet", hp, Hex(new HealthStatusPacket { CurrentHp = (uint)hp }.EncodePayload()), Hex(new ManaStatusPacket { CurrentMp = (uint)hp }.EncodePayload()));
            yield return Join("revival", hp, Hex(PlayerRevivalFrames.RestartNotice(9, (uint)hp)));
        }
        var block = new CharacterStatBlock(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 4, 2, 7, 33, CharacterStatBlock.LoginMarkWhenUnset);
        foreach (var value in new[] { 0u, 1u, 250u, 5000u, 1_234_567u, 2_000_000_000u })
        {
            yield return Join("progress", value, Hex(ProgressFrames.ExpGained(value)), Hex(ProgressFrames.TmTotal(0, value)),
                Hex(ProgressFrames.TmLevelUp(1, 55, value, 3)), Hex(PlayerDeathFrames.ExpValue(value)), Hex(GroundItemFrames.Money(value)));
            yield return Join("bank", value, Hex(BankFrames.Money(value, value / 2)));
            yield return Join("shop", value, Hex(ShopFrames.Traded(value, Array.Empty<BagOperation>())));
            yield return Join("trade", value, Hex(TradeFrames.Settled(value, Array.Empty<BagOperation>())));
            yield return Join("quest", value, Hex(QuestResultFrames.Result(Array.Empty<BagOperation>(), Array.Empty<(uint, ushort)>(), value, value / 2, 12, 3, value / 3)),
                Hex(QuestResultFrames.PartyQuestResult(Array.Empty<BagOperation>(), value, value / 2, 99)));
            yield return Join("level-up", value, Hex(ProgressFrames.LevelUp(block, value, value / 2, value)));
        }
        foreach (var money in new[] { 0u, 4500u, 2_000_000_000u })
        {
            var info = new tPoseidon.Kr2014.Wire.CharInfoPacket
            {
                Stats = block,
                MapId = 11,
                PositionX = 100,
                PositionY = 200,
                ReturnMapId = 11,
                Money = money,
                Hp = 1234,
                Mp = 567,
                TmExp = 890,
                Exp = 4321,
                Hair = 3,
                WarehouseSlots = 80,
            };
            yield return Join("char-info", money, Hex(info.EncodePayload()));
        }
    }
}
