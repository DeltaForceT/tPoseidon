using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SpecialItemUseTests
{
    private const ushort Refused = 0x005A;
    private const ushort Effect = 0x0097;
    private const ushort Notice = 0x01E4;
    private const ushort BlockEnd = 0x079F;
    private const ushort MapRefusal = 0x031F;

    private static async Task<(SpecialItemHarness Harness, Item Item)> UsedAsync(int effect, byte[]? data, Action<SpecialItemHarness>? arrange = null, ushort stack = 1, int options = ItemParamEntry.UsableOption | SpecialItemEffects.CustomDataOption)
    {
        var harness = new SpecialItemHarness();
        harness.DefineSpecial(effect, options: options);
        arrange?.Invoke(harness);
        var item = harness.Give(stack: stack);
        harness.Build();
        await harness.UseWithDataAsync(item, data ?? []);
        return (harness, item);
    }

    private static byte[] Le(params ushort[] words) => words.SelectMany(BitConverter.GetBytes).ToArray();

    [Fact]
    public async Task AnItemWithoutCustomDataIsConsumedWithTheEffectAnswer()
    {
        var (harness, item) = await UsedAsync(35, null, options: ItemParamEntry.UsableOption);

        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.Equal(new[] { BlockEnd }, harness.AreaWords);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task AKeptItemStaysInTheBagWhenUsed()
    {
        var harness = new SpecialItemHarness();
        harness.Define(new ItemParamEntry { Id = SpecialItemHarness.ItemId, Type = 15, Effect = 35, Options = ItemParamEntry.UsableOption, ExtendedOptions = ItemParamEntry.KeptWhenUsedExtendedOption });
        var item = harness.Give();
        harness.Build();

        await harness.UseAsync(item);

        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task ACustomDataEffectWithoutTheOptionBitIsTheDefaultBranch()
    {
        var (harness, item) = await UsedAsync(15, [3], options: ItemParamEntry.UsableOption);

        Assert.Empty(harness.State.DyedColors);
        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task ADeadPlayerUsesNothing()
    {
        var (harness, item) = await UsedAsync(35, null, h => h.State.Dead = true, options: ItemParamEntry.UsableOption);

        Assert.Empty(harness.Sent);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task HairDyeStoresTheColourAnswersTheUserAndTheAreaAndConsumesOne()
    {
        var (harness, item) = await UsedAsync(15, [7], stack: 2);

        Assert.Equal(new byte[] { 7 }, harness.State.DyedColors);
        Assert.Equal(new ushort[] { 0x0151, Effect }, harness.SentWords);
        Assert.Equal(new byte[] { 7 }, harness.Sent[0].Payload);
        Assert.Equal(new ushort[] { 0x0152, BlockEnd }, harness.AreaWords);
        Assert.Equal(new byte[] { 0x23, 0x01, 7 }, harness.Area[0].Payload);
        Assert.Equal(1, harness.StackOf(item));
    }

    [Fact]
    public async Task HairDyeRefusesAnotherFormWithTheFormNoticeAndKeepsTheItem()
    {
        var (harness, item) = await UsedAsync(15, [7], h => h.State.NormalForm = false);

        Assert.Equal(new[] { Notice }, harness.SentWords);
        Assert.Equal(new byte[] { 8, 0, 0, 0, 0 }, harness.Sent[0].Payload);
        Assert.Empty(harness.State.DyedColors);
        Assert.True(harness.InBag(item));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x15 })]
    [InlineData(new byte[] { 0xFF })]
    public async Task HairDyeRefusesAMissingOrOutOfRangeColour(byte[] data)
    {
        var (harness, item) = await UsedAsync(15, data);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.Empty(harness.State.DyedColors);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task HairDyeKeepsTheItemWhenTheColourIsNotStored()
    {
        var (harness, item) = await UsedAsync(15, [4], h => h.State.StoreSucceeds = false);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGenderChangeTurnsTheTypeIntoItsNeighbourAndAnswersTheArea()
    {
        var (harness, item) = await UsedAsync(21, Le(1));

        Assert.Equal(new byte[] { 5 }, harness.State.GenderTypes);
        Assert.Equal(new ushort[] { Effect }, harness.SentWords);
        Assert.Equal(new ushort[] { 0x0248, 0x0246, BlockEnd }, harness.AreaWords);
        Assert.Equal(new byte[] { 0x23, 0x01, 5, 0, 5, 0 }, harness.Area[0].Payload);
        Assert.False(harness.InBag(item));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 9, 0 })]
    public async Task TheGenderChangeRefusesAMissingOrTooBigValue(byte[] data)
    {
        var (harness, item) = await UsedAsync(21, data);

        Assert.Equal(new ushort[] { 0x0249, Refused }, harness.SentWords);
        Assert.Empty(harness.State.GenderTypes);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGenderChangeRefusesAfterAJobChange()
    {
        var (harness, item) = await UsedAsync(21, Le(1), h => h.Character = new Character
        {
            CharacterId = SpecialItemHarness.CharacterId, OwnerAccountId = 5, Slot = 0, Name = "Tester", Species = Species.Lion, FType = FType.Power, Job2 = 10,
        });

        Assert.Equal(new ushort[] { 0x0249, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGenderChangeRefusesTheSeventhSpeciesNeighbourlessType()
    {
        var (harness, item) = await UsedAsync(21, Le(1), h => h.Character = new Character
        {
            CharacterId = SpecialItemHarness.CharacterId, OwnerAccountId = 5, Slot = 0, Name = "Tester", Species = Species.PolarBear, FType = FType.Power,
        });

        Assert.Equal(new ushort[] { 0x0249, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGenderChangeRefusesWhenTheBagCannotTakeTheWornAndStarterItems()
    {
        var harness = new SpecialItemHarness();
        harness.DefineSpecial(21);
        for (byte slot = 0; slot < 4; slot++) harness.Give(5000 + slot, 1, SlotKind.Equipment, slot);
        for (var filler = 0; filler < tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity - 5; filler++) harness.Give(6000 + filler, 1, SlotKind.Inventory, 0);
        var item = harness.Give();
        harness.Build();

        await harness.UseWithDataAsync(item, Le(1));

        Assert.Equal(new ushort[] { 0x0249, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGenderChangeRefusesAnotherFormAndAFailedStore()
    {
        var (form, formItem) = await UsedAsync(21, Le(1), h => h.State.NormalForm = false);
        var (failed, failedItem) = await UsedAsync(21, Le(1), h => h.State.StoreSucceeds = false);

        Assert.Equal(new[] { Notice }, form.SentWords);
        Assert.True(form.InBag(formItem));
        Assert.Equal(new ushort[] { 0x0249, Refused }, failed.SentWords);
        Assert.True(failed.InBag(failedItem));
    }

    [Fact]
    public async Task TheSkillResetForgetsTheSkillAndReturnsTheRefund()
    {
        var (harness, item) = await UsedAsync(23, Le(1104), h =>
        {
            h.State.Skills = new Dictionary<int, int> { [1104] = 5 };
            h.State.Cards[1104] = new SkillCardEntry { Id = 1, AffectionSkill = 1104, AffectionPoint = 3, RegisterLevel = 1 };
        });

        Assert.Equal(new[] { (1104, 7) }, harness.State.ForgottenSkills);
        Assert.Equal(new ushort[] { 0x026A, Effect }, harness.SentWords);
        Assert.Equal(Le(1104, 7), harness.Sent[0].Payload);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task TheSkillResetAddsTheMasteryCostForAMasteredSkill()
    {
        var (harness, _) = await UsedAsync(23, Le(1104), h =>
        {
            h.State.Skills = new Dictionary<int, int> { [1104] = 11 };
            h.State.Cards[1104] = new SkillCardEntry { Id = 1, AffectionSkill = 1104, AffectionPoint = 3, RegisterLevel = 1 };
            h.State.MasterTm[1104] = 8;
        });

        Assert.Equal(new[] { (1104, 20) }, harness.State.ForgottenSkills);
    }

    [Fact]
    public async Task TheSkillResetRefusesAShortRequestWithoutAnAnswerOfItsOwn()
    {
        var (harness, item) = await UsedAsync(23, [1]);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheSkillResetRefusesSpecialPrerequisiteUnownedAndCardlessSkills()
    {
        var cards = new Dictionary<int, SkillCardEntry>
        {
            [1102] = new() { Id = 1, AffectionSkill = 1102, PreSkills = [new SkillRequirement(1101, 3)] },
            [1101] = new() { Id = 2, AffectionSkill = 1101 },
        };
        foreach (var skill in new ushort[] { 5001, 1101, 1300, 1400 })
        {
            var (harness, item) = await UsedAsync(23, Le(skill), h =>
            {
                h.State.Skills = new Dictionary<int, int> { [1101] = 2, [1102] = 2, [5001] = 1, [1400] = 2 };
                foreach (var (id, card) in cards) h.State.Cards[id] = card;
            });

            Assert.Equal(new ushort[] { 0x026C, Refused }, harness.SentWords);
            Assert.Empty(harness.State.ForgottenSkills);
            Assert.True(harness.InBag(item));
        }
    }

    [Fact]
    public async Task TheSkillResetKeepsTheItemWhenTheStoreFails()
    {
        var (harness, item) = await UsedAsync(23, Le(1104), h =>
        {
            h.State.StoreSucceeds = false;
            h.State.Skills = new Dictionary<int, int> { [1104] = 5 };
            h.State.Cards[1104] = new SkillCardEntry { Id = 1, AffectionSkill = 1104 };
        });

        Assert.Equal(new ushort[] { 0x026C, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    private static void StatArrangement(SpecialItemHarness harness)
    {
        harness.State.Points = new short[] { 50, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30 };
        harness.State.Weights = new short[] { 4, 2, 1, 3 };
        harness.State.Bonus = 5;
    }

    private static SpecialItemHarness LevelEleven(Action<SpecialItemHarness>? arrange = null)
    {
        var harness = new SpecialItemHarness(level: 11);
        StatArrangement(harness);
        arrange?.Invoke(harness);
        return harness;
    }

    private static async Task<(SpecialItemHarness Harness, Item Item)> UsedAtLevelElevenAsync(int effect, byte[]? data, Action<SpecialItemHarness>? arrange = null)
    {
        var harness = LevelEleven(arrange);
        harness.DefineSpecial(effect);
        var item = harness.Give();
        harness.Build();
        await harness.UseWithDataAsync(item, data ?? []);
        return (harness, item);
    }

    [Fact]
    public async Task APointBackReturnsThePointsAsBonusAndAnswersTheStatBlock()
    {
        var take = new byte[12];
        take[0] = 10;

        var (harness, item) = await UsedAtLevelElevenAsync(24, take);

        var applied = Assert.Single(harness.State.AppliedPoints);
        Assert.Equal(40, applied.Points[0]);
        Assert.Equal(15, applied.Bonus);
        Assert.Equal(new ushort[] { 0x0275, Effect }, harness.SentWords);
        Assert.Equal(CharacterStatBlock.Size, harness.Sent[0].Payload.Length);
        Assert.Equal(15, BitConverter.ToUInt16(harness.Sent[0].Payload, 24));
        Assert.False(harness.InBag(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public async Task APointBackRefusesAnAmountOutsideOneToForty(int total)
    {
        var take = new byte[12];
        take[0] = (byte)total;

        var (harness, item) = await UsedAtLevelElevenAsync(24, take, h => h.State.Points = Enumerable.Repeat((short)200, 12).ToArray());

        Assert.Equal(new ushort[] { 0x0277, Refused }, harness.SentWords);
        Assert.Empty(harness.State.AppliedPoints);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task APointBackBelowTheBaselineAnswersTheRefusalTwiceLikeTheKrServer()
    {
        var take = new byte[12];
        take[3] = 5;

        var (harness, item) = await UsedAtLevelElevenAsync(24, take);

        Assert.Equal(new ushort[] { 0x0277, 0x0277, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task APointBackRefusesAShortRequestAndAnotherForm()
    {
        var (shortRequest, shortItem) = await UsedAtLevelElevenAsync(24, new byte[11]);
        var (form, formItem) = await UsedAtLevelElevenAsync(24, new byte[12], h => h.State.NormalForm = false);

        Assert.Equal(new[] { Refused }, shortRequest.SentWords);
        Assert.True(shortRequest.InBag(shortItem));
        Assert.Equal(new[] { Notice }, form.SentWords);
        Assert.True(form.InBag(formItem));
    }

    [Fact]
    public async Task APointBackKeepsTheItemWhenTheStoreFails()
    {
        var take = new byte[12];
        take[0] = 1;

        var (harness, item) = await UsedAtLevelElevenAsync(24, take, h => h.State.StoreSucceeds = false);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task APointBackAllResetsEveryStatAndAnswersTheRefund()
    {
        var (harness, item) = await UsedAtLevelElevenAsync(26, null);

        var applied = Assert.Single(harness.State.AppliedPoints);
        Assert.Equal(new short[] { 40, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30 }, applied.Points);
        Assert.Equal(15, applied.Bonus);
        Assert.Equal(new ushort[] { 0x027A, Effect }, harness.SentWords);
        Assert.Equal(CharacterStatBlock.Size + 4, harness.Sent[0].Payload.Length);
        Assert.Equal(10u, BitConverter.ToUInt32(harness.Sent[0].Payload, CharacterStatBlock.Size));
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task APointBackAllWithNothingToTakeBackAnswersBothRefusals()
    {
        var (harness, item) = await UsedAtLevelElevenAsync(26, null, h => h.State.Points = new short[] { 40, 40, 40, 20, 20, 20, 10, 10, 10, 30, 30, 30 });

        Assert.Equal(new ushort[] { 0x027C, 0x0277, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task APointBackAllRefusesAnotherForm()
    {
        var (harness, item) = await UsedAtLevelElevenAsync(26, null, h => h.State.NormalForm = false);

        Assert.Equal(new[] { Notice }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGrowthGraphResetStoresTheWeightsAndAnswersCharacterInfoAndStatBlock()
    {
        var (harness, item) = await UsedAtLevelElevenAsync(25, [4, 3, 2, 1]);

        var applied = Assert.Single(harness.State.AppliedGrowth);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, applied.Weights);
        Assert.Equal(new short[] { 40, 40, 40, 30, 30, 30, 20, 20, 20, 10, 10, 10 }, applied.Points);
        Assert.Equal(40, applied.Bonus);
        Assert.Equal(new ushort[] { 0x0289, Effect }, harness.SentWords);
        Assert.Equal(69 + CharacterStatBlock.Size, harness.Sent[0].Payload.Length);
        Assert.False(harness.InBag(item));
    }

    [Theory]
    [InlineData(new byte[] { 3, 4, 2, 1 })]
    [InlineData(new byte[] { 4, 3, 2, 2 })]
    [InlineData(new byte[] { 4, 5, 1, 0 })]
    [InlineData(new byte[] { 4, 3, 3 })]
    [InlineData(new byte[0])]
    public async Task TheGrowthGraphResetRefusesBadWeights(byte[] weights)
    {
        var (harness, item) = await UsedAtLevelElevenAsync(25, weights);

        Assert.Equal(new ushort[] { 0x028B, Refused }, harness.SentWords);
        Assert.Empty(harness.State.AppliedGrowth);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheGrowthGraphResetRefusesAnotherFormAndAFailedStore()
    {
        var (form, formItem) = await UsedAtLevelElevenAsync(25, [4, 3, 2, 1], h => h.State.NormalForm = false);
        var (failed, failedItem) = await UsedAtLevelElevenAsync(25, [4, 3, 2, 1], h => h.State.StoreSucceeds = false);

        Assert.Equal(new[] { Notice }, form.SentWords);
        Assert.True(form.InBag(formItem));
        Assert.Equal(new[] { Refused }, failed.SentWords);
        Assert.True(failed.InBag(failedItem));
    }

    [Fact]
    public async Task TheStoragePermitAddsEightSlotsAndAnswersThem()
    {
        var (harness, item) = await UsedAsync(27, null, stack: 2);

        Assert.Equal(8, Assert.Single(harness.State.StorageGrowth));
        Assert.Equal(new ushort[] { Effect, 0x02E6 }, harness.SentWords);
        Assert.Equal(new byte[] { 8 }, harness.Sent[1].Payload);
        Assert.Equal(1, harness.StackOf(item));
    }

    [Fact]
    public async Task TheStoragePermitRefusesAtTheLimitAndKeepsTheItem()
    {
        var (harness, item) = await UsedAsync(27, null, h => h.State.WarehouseSlots = 800);

        Assert.Equal(new ushort[] { 0x02E7, Refused }, harness.SentWords);
        Assert.Empty(harness.State.StorageGrowth);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheStoragePermitKeepsTheItemWhenTheStoreFails()
    {
        var (harness, item) = await UsedAsync(27, null, h => h.State.StoreSucceeds = false);

        Assert.Equal(new ushort[] { 0x02E7, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task ARenameStoresTheNameAndAnswersTheRemovedItemRecord()
    {
        var (harness, item) = await UsedAsync(31, Encoding.ASCII.GetBytes("Newname\0"));

        Assert.Equal(new[] { ("Newname", SpecialItemHarness.ItemId) }, harness.State.Renames);
        Assert.Equal(new ushort[] { 0x0314 }, harness.SentWords);
        var record = QuestResultFrames.ReadItemOperation(harness.Sent[0].Payload);
        Assert.Equal(QuestResultFrames.Removed, record.Kind);
        Assert.Equal((uint)SpecialItemHarness.ItemId, record.ItemId);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task ARenameOfAStackAnswersTheNewCount()
    {
        var (harness, item) = await UsedAsync(31, Encoding.ASCII.GetBytes("Newname\0"), stack: 3);

        var record = QuestResultFrames.ReadItemOperation(harness.Sent[0].Payload);
        Assert.Equal(QuestResultFrames.StackChanged, record.Kind);
        Assert.Equal(2, record.Count);
        Assert.Equal(2, harness.StackOf(item));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a b c d")]
    [InlineData(".dotted")]
    [InlineData("com,ma")]
    public async Task ARenameWithAForbiddenNameAnswersTheInvalidNameCode(string name)
    {
        var (harness, item) = await UsedAsync(31, Encoding.ASCII.GetBytes(name + "\0"));

        Assert.Equal(new ushort[] { 0x0315, Refused }, harness.SentWords);
        Assert.Equal(SpecialItemFrames.Code(1019), harness.Sent[0].Payload);
        Assert.Empty(harness.State.Renames);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task ARenameOfSeventeenBytesWithoutATerminatorIsTooLongForTheNameRule()
    {
        var (harness, item) = await UsedAsync(31, Encoding.ASCII.GetBytes("12345678901234567"));

        Assert.Equal(new ushort[] { 0x0315, Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    public async Task ARenameRefusesAnEmptyOrTooLongNameField(int length)
    {
        var (harness, item) = await UsedAsync(31, new byte[length]);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }

    [Theory]
    [InlineData(1023u)]
    [InlineData(1019u)]
    [InlineData(101u)]
    public async Task ARenameTheStoreRefusesAnswersItsCodeAndKeepsTheItem(uint code)
    {
        var (harness, item) = await UsedAsync(31, Encoding.ASCII.GetBytes("Newname\0"), h => h.State.RenameCode = code);

        Assert.Equal(new ushort[] { 0x0315 }, harness.SentWords);
        Assert.Equal(SpecialItemFrames.Code(code), harness.Sent[0].Payload);
        Assert.True(harness.InBag(item));
    }

    private static byte[] MegaphoneRequest(ulong microphone, string name, string text) =>
        BitConverter.GetBytes(microphone).Concat(Encoding.ASCII.GetBytes($"{name}\0{text}\0")).ToArray();

    private static async Task<(SpecialItemHarness Harness, Item Megaphone, Item? Microphone)> ShoutAsync(
        Func<SpecialItemHarness, Item?, byte[]> request, int microphoneEffect = SpecialItemEffects.Microphone, bool giveMicrophone = true, Action<SpecialItemHarness>? arrange = null)
    {
        var harness = new SpecialItemHarness();
        harness.DefineSpecial(32);
        harness.Define(new ItemParamEntry { Id = SpecialItemHarness.MicrophoneItemId, Type = 15, Effect = microphoneEffect, Options = ItemParamEntry.UsableOption, Class = 1 });
        arrange?.Invoke(harness);
        var megaphone = harness.Give();
        var microphone = giveMicrophone ? harness.Give(SpecialItemHarness.MicrophoneItemId, 1, SlotKind.Inventory, 1) : null;
        harness.Build();
        await harness.UseWithDataAsync(megaphone, request(harness, microphone));
        return (harness, megaphone, microphone);
    }

    [Fact]
    public async Task AMegaphoneShoutsTheTextConsumesTheMegaphoneAndKeepsTheMicrophone()
    {
        var (harness, megaphone, microphone) = await ShoutAsync(
            (h, mic) => MegaphoneRequest(SpecialItemHarness.WireOf(mic!), "Tester", "Hello all"), arrange: h => h.State.ShoutQueue = 3);

        Assert.Equal("Hello all", Assert.Single(harness.State.Shouts));
        Assert.Equal(new ushort[] { 0x0322 }, harness.SentWords);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, harness.Sent[0].Payload[^4..]);
        Assert.False(harness.InBag(megaphone));
        Assert.True(harness.InBag(microphone!));
    }

    [Fact]
    public async Task AMegaphoneNeedsAMicrophoneInTheBag()
    {
        var (harness, megaphone, _) = await ShoutAsync((_, _) => MegaphoneRequest(0x1_0000_9999, "Tester", "Hello"), giveMicrophone: false);

        Assert.Equal(new ushort[] { 0x0323, Refused }, harness.SentWords);
        Assert.Equal(SpecialItemFrames.Code(1), harness.Sent[0].Payload);
        Assert.Empty(harness.State.Shouts);
        Assert.True(harness.InBag(megaphone));
    }

    [Fact]
    public async Task AMegaphoneRefusesAnItemWhichIsNotAMicrophone()
    {
        var (harness, megaphone, _) = await ShoutAsync((h, mic) => MegaphoneRequest(SpecialItemHarness.WireOf(mic!), "Tester", "Hello"), microphoneEffect: 35);

        Assert.Equal(new ushort[] { 0x0323, Refused }, harness.SentWords);
        Assert.True(harness.InBag(megaphone));
    }

    [Fact]
    public async Task AMegaphoneRefusesATextOfMoreThanOneHundredSixtyOneBytes()
    {
        var (harness, megaphone, _) = await ShoutAsync((h, mic) => MegaphoneRequest(SpecialItemHarness.WireOf(mic!), "Tester", new string('x', 162)));

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(megaphone));
    }

    [Fact]
    public async Task AMegaphoneAcceptsATextOfExactlyOneHundredSixtyOneBytes()
    {
        var (harness, megaphone, _) = await ShoutAsync((h, mic) => MegaphoneRequest(SpecialItemHarness.WireOf(mic!), "Tester", new string('x', 161)));

        Assert.Single(harness.State.Shouts);
        Assert.False(harness.InBag(megaphone));
    }

    [Fact]
    public async Task AMegaphoneRefusesARequestWithoutNameAndText()
    {
        var (harness, megaphone, _) = await ShoutAsync((h, mic) => BitConverter.GetBytes(SpecialItemHarness.WireOf(mic!)));

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(megaphone));
    }

    [Fact]
    public async Task AMegaphoneNotBroadcastAnswersTheCancelAndKeepsTheItem()
    {
        var (harness, megaphone, _) = await ShoutAsync(
            (h, mic) => MegaphoneRequest(SpecialItemHarness.WireOf(mic!), "Tester", "Hello"), arrange: h => h.State.ShoutQueue = null);

        Assert.Equal(new ushort[] { 0x0323 }, harness.SentWords);
        Assert.True(harness.InBag(megaphone));
    }

    [Fact]
    public async Task ThePortablePortSendsTheUserToAZoneFromOneToSevenWithoutAPosition()
    {
        var (harness, item) = await UsedAsync(14, Le(3));

        Assert.Equal(new[] { new WarpCall(3, null) }, harness.State.Warps);
        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.False(harness.InBag(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(1000)]
    public async Task ThePortablePortRefusesAZoneOutsideOneToSeven(ushort zone)
    {
        var (harness, item) = await UsedAsync(14, Le(zone));

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.Empty(harness.State.Warps);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task ThePortablePortRefusesAShortRequestAndAForbiddingMap()
    {
        var (shortRequest, shortItem) = await UsedAsync(14, [3]);
        var (mapRefused, mapItem) = await UsedAsync(14, Le(3), h => h.State.MapOptions[148] = 0x80);

        Assert.Equal(new[] { Refused }, shortRequest.SentWords);
        Assert.True(shortRequest.InBag(shortItem));
        Assert.Equal(new[] { MapRefusal, Refused }, mapRefused.SentWords);
        Assert.Equal(SpecialItemFrames.Code(4), mapRefused.Sent[0].Payload);
        Assert.Empty(mapRefused.State.Warps);
        Assert.True(mapRefused.InBag(mapItem));
    }

    [Fact]
    public async Task TheEmergencyPortSendsTheUserToTheReturnMap()
    {
        var (harness, item) = await UsedAsync(19, null, options: ItemParamEntry.UsableOption);

        Assert.Equal(new[] { new WarpCall(33, null) }, harness.State.Warps);
        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task TheEmergencyPortRefusesTheReturnMapItself()
    {
        var (harness, item) = await UsedAsync(19, null, h => h.State.ReturnMap = 148, options: ItemParamEntry.UsableOption);

        Assert.Equal(new ushort[] { 0x0235, Refused }, harness.SentWords);
        Assert.Empty(harness.State.Warps);
        Assert.True(harness.InBag(item));
    }

    [Fact]
    public async Task TheEmergencyPortRefusesAForbiddingMapAndAReturnMapOutsideTheZones()
    {
        var (mapRefused, mapItem) = await UsedAsync(19, null, h => h.State.MapOptions[148] = 0x80, options: ItemParamEntry.UsableOption);
        var (outside, outsideItem) = await UsedAsync(19, null, h => h.State.ReturnMap = 0, options: ItemParamEntry.UsableOption);

        Assert.Equal(new[] { MapRefusal, Refused }, mapRefused.SentWords);
        Assert.True(mapRefused.InBag(mapItem));
        Assert.Equal(new[] { Refused }, outside.SentWords);
        Assert.True(outside.InBag(outsideItem));
    }

    private static SpecialPortTables Ports() => new(
        new Dictionary<int, TeleportAdvEntry> { [2] = new(2, 0, 77), [3] = new(3, 0, 148) },
        new Dictionary<int, RandomPortEntry> { [SpecialItemHarness.ItemId] = new(SpecialItemHarness.ItemId, "Off we go", [new RandomPortZone(54, 20)]) });

    [Fact]
    public async Task ThePortableAdvancedPortSendsTheUserToTheListedZone()
    {
        var (harness, item) = await UsedAsync(20, BitConverter.GetBytes(2u), h => h.State.Ports = Ports());

        Assert.Equal(new[] { new WarpCall(77, null) }, harness.State.Warps);
        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task ThePortableAdvancedPortRefusesAnUnlistedIndexTheCurrentZoneAShortRequestAndAForbiddingMap()
    {
        var (unlisted, unlistedItem) = await UsedAsync(20, BitConverter.GetBytes(9u), h => h.State.Ports = Ports());
        var (same, sameItem) = await UsedAsync(20, BitConverter.GetBytes(3u), h => h.State.Ports = Ports());
        var (shortRequest, shortItem) = await UsedAsync(20, [2, 0], h => h.State.Ports = Ports());
        var (mapRefused, mapItem) = await UsedAsync(20, BitConverter.GetBytes(2u), h =>
        {
            h.State.Ports = Ports();
            h.State.MapOptions[148] = 0x80;
        });

        Assert.Equal(new ushort[] { 0x0237, Refused }, unlisted.SentWords);
        Assert.Equal(new ushort[] { 0x0236, Refused }, same.SentWords);
        Assert.Equal(new[] { Refused }, shortRequest.SentWords);
        Assert.Equal(new[] { MapRefusal, Refused }, mapRefused.SentWords);
        foreach (var (refused, item) in new[] { (unlisted, unlistedItem), (same, sameItem), (shortRequest, shortItem), (mapRefused, mapItem) })
        {
            Assert.Empty(refused.State.Warps);
            Assert.True(refused.InBag(item));
        }
    }

    [Fact]
    public async Task TheBroomAnnouncesItsNoticeThenSendsTheUserToTheRolledZone()
    {
        var (harness, item) = await UsedAsync(28, null, h => h.State.Ports = Ports(), options: ItemParamEntry.UsableOption);

        Assert.Equal(new ushort[] { 0x02F6, Effect }, harness.SentWords);
        Assert.Equal(Encoding.ASCII.GetBytes("Off we go\0"), harness.Sent[0].Payload);
        Assert.Equal(new[] { new WarpCall(54, null) }, harness.State.Warps);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task TheBroomRefusesAMissingRowAndAForbiddingMap()
    {
        var (missing, missingItem) = await UsedAsync(28, null, options: ItemParamEntry.UsableOption);
        var (mapRefused, mapItem) = await UsedAsync(28, null, h =>
        {
            h.State.Ports = Ports();
            h.State.MapOptions[148] = 0x80;
        }, options: ItemParamEntry.UsableOption);

        Assert.Equal(new[] { Refused }, missing.SentWords);
        Assert.True(missing.InBag(missingItem));
        Assert.Equal(new[] { MapRefusal, Refused }, mapRefused.SentWords);
        Assert.True(mapRefused.InBag(mapItem));
    }

    private static byte[] Friend(short x, short y, ushort map) =>
        BitConverter.GetBytes(x).Concat(BitConverter.GetBytes(y)).Concat(BitConverter.GetBytes(map)).ToArray();

    [Fact]
    public async Task TheFriendFinderSendsTheUserToTheFriendsPosition()
    {
        var (harness, item) = await UsedAsync(39, Friend(120, 340, 69));

        Assert.Equal(new[] { new WarpCall(69, (120, 340)) }, harness.State.Warps);
        Assert.Equal(new[] { Effect }, harness.SentWords);
        Assert.False(harness.InBag(item));
    }

    [Fact]
    public async Task TheFriendFinderDropsAnUnusablePositionAndKeepsTheZone()
    {
        var (harness, _) = await UsedAsync(39, Friend(-3, 340, 69));

        Assert.Equal(new[] { new WarpCall(69, null) }, harness.State.Warps);
    }

    [Fact]
    public async Task TheFriendFinderRefusesABusyPlayerAShortRequestAndAZoneOutsideTheRange()
    {
        var (busy, busyItem) = await UsedAsync(39, Friend(1, 1, 69), h => h.State.Idle = false);
        var (shortRequest, shortItem) = await UsedAsync(39, new byte[5]);
        var (outside, outsideItem) = await UsedAsync(39, Friend(1, 1, 1000));

        foreach (var (refused, item) in new[] { (busy, busyItem), (shortRequest, shortItem), (outside, outsideItem) })
        {
            Assert.Equal(new[] { Refused }, refused.SentWords);
            Assert.Empty(refused.State.Warps);
            Assert.True(refused.InBag(item));
        }
    }

    [Fact]
    public async Task TheFriendFinderRefusesAForbiddingCurrentMapAndAForbiddingDestination()
    {
        var (current, currentItem) = await UsedAsync(39, Friend(1, 1, 69), h => h.State.MapOptions[148] = 0x80);
        var (destination, destinationItem) = await UsedAsync(39, Friend(1, 1, 69), h => h.State.MapOptions[69] = 0x400);

        Assert.Equal(new[] { MapRefusal, Refused }, current.SentWords);
        Assert.Equal(new[] { MapRefusal, Refused }, destination.SentWords);
        Assert.True(current.InBag(currentItem));
        Assert.True(destination.InBag(destinationItem));
        Assert.Empty(destination.State.Warps);
    }

    [Fact]
    public async Task AnItemWhoseEffectHasNoServerCodeKeepsRefusing()
    {
        var harness = new SpecialItemHarness();
        harness.DefineSpecial(44, options: ItemParamEntry.UsableOption);
        var item = harness.Give();
        harness.Build();

        await harness.UseAsync(item);

        Assert.Equal(new[] { Refused }, harness.SentWords);
        Assert.True(harness.InBag(item));
    }
}
