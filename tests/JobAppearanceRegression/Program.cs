using System.Buffers.Binary;
using System.Reflection;
using System.Text.Json;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Kr2014.GameServer;
using tPoseidon.Kr2014.Wire;

var checks = 0;
var failures = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) { failures++; Console.WriteLine("FAIL " + name); }
}
var restore = typeof(EnterWorldBatchSynthesizer).GetMethod("RestoreLoginAppearance", BindingFlags.Static | BindingFlags.NonPublic);
byte[] Apply(byte[] payload, Character character, bool stats) => restore is null
    ? payload
    : (byte[])(restore.Invoke(null, new object[] { payload, character, stats }) ?? throw new InvalidOperationException("No appearance payload."));
foreach (var tier in new byte[] { 1, 2, 3 })
{
    foreach (var look in Enumerable.Range(1, tier).Select(i => (byte)i))
    {
        var original = new Character
        {
            CharacterId = 1, OwnerAccountId = 1, Slot = 0, Name = "Test",
            Species = (Species)6, FType = (FType)1, Level = 60,
            JobTier = tier, JobState = look, Job2 = tier >= 2 ? (byte)15 : (byte)0,
            Job3 = tier >= 3 ? (byte)24 : (byte)0,
            Hair = 2, Hair2 = 4, Hair3 = 7, WarehouseSlots = 80,
        };
        // Synthetic persistence round trip, not a live database test.
        var character = JsonSerializer.Deserialize<Character>(JsonSerializer.Serialize(original))
            ?? throw new InvalidOperationException("Character round trip failed.");
        var dataBefore = GameCharDataPacket.For(character).EncodePayload();
        var data = Apply((byte[])dataBefore.Clone(), character, false);
        var info = EnterWorldBatchSynthesizer.BuildCharInfo(character);
        var statsBefore = info.EncodePayload();
        var stats = Apply((byte[])statsBefore.Clone(), character, true);
        Check(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x35)) == OccupationRules.SecondJobTypeOf(character), "second body");
        Check(data[0x37] == character.Job2, "second job");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x38)) == OccupationRules.ThirdJobTypeOf(character), "third body fallback");
        Check(data[0x3A] == character.Job3, "third job");
        Check(stats[271] == 2 && stats[272] == 4 && stats[273] == look && stats[276] == 7, "all hair slots and look tier");
        Check(stats[274] == statsBefore[274] && stats[275] == statsBefore[275], "warehouse preserved");
        Check(data.AsSpan(0, 0x35).SequenceEqual(dataBefore.AsSpan(0, 0x35)) && data.AsSpan(0x3B).SequenceEqual(dataBefore.AsSpan(0x3B)), "unrelated char data preserved");
        Check(stats.AsSpan(0, 271).SequenceEqual(statsBefore.AsSpan(0, 271)), "stats and equipment preserved");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x25)) == 6, "species is not overwritten by advanced job");
        var makeUp = LookChangeRules.MakeUpOf(character, look) ?? throw new InvalidOperationException("Missing look.");
        var spawn = PlayerSpawnLoginPacket.FromCharacter(character, 10);
        var warp = PlayerSpawnLoginPacket.FromCharacter(character, 10, null, (101, 202));
        Check(spawn.BodyType == makeUp.Type && spawn.Hair == makeUp.Hair, "initial spawn look");
        Check(warp.BodyType == spawn.BodyType && warp.Hair == spawn.Hair && warp.ClassChainId == LookChangeRules.HighestJobOf(character), "warp preserves appearance and highest job");
        var wire = warp.EncodePayload();
        Check(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(4)) == makeUp.Type && wire[6] == makeUp.Hair && wire[7] == LookChangeRules.HighestJobOf(character), "spawn wire fields");
        var selection = CharacterListBuilder.Build(new[] { character });
        Check(selection.Entries[0].Raw.Span[0x24] == Math.Max(character.JobTier, look), "selection tier contract preserved");
        Check(OccupationRules.HeldTierOf(character) == tier && OccupationRules.LookStateOf(character) == look, "held tier distinct from selected look");
        Check(Apply(GameCharDataPacket.For(character).EncodePayload(), character, false).SequenceEqual(data), "reconnect deterministic");
    }
}
Console.WriteLine($"{checks} checks, {failures} failures; synthetic state and packet tests only.");
Environment.ExitCode = failures == 0 ? 0 : 1;
