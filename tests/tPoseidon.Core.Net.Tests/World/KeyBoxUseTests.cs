using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class KeyBoxUseTests
{
    private const ushort Opened = 0x0513;
    private const ushort OpenRefused = 0x0514;
    private const ushort Closed = 0x0516;
    private const ushort Result = 0x0510;
    private const ushort ConfirmRefused = 0x0511;
    private const int BoxId = 431827;
    private const int KeyId = 19839;
    private const int OtherKeyId = 19837;
    private const int NormalPresent = 431830;
    private const int RarePresentA = 431838;
    private const int RarePresentB = 431843;
    private const int NormalPrize = 777;
    private const int PrizeA = 19832;
    private const int PrizeB = 441093;

    private static PresentItemEntry Present(int id, int itemId) =>
        new(id, PresentRolls.WeightedType, 1, [new PresentDrop(itemId, 100, 1)]);

    private static SpecialItemHarness Harness(bool onlyKey, int rareBRate, int? keyRate, Action<SpecialItemHarness>? arrange = null)
    {
        var harness = new SpecialItemHarness();
        harness.Define(new ItemParamEntry { Id = BoxId, Type = 15, Effect = KeyBoxRules.BoxEffect, Options = ItemParamEntry.UsableOption, Class = 1 });
        harness.Define(new ItemParamEntry { Id = KeyId, Type = 15, Effect = KeyBoxRules.KeyEffect, Options = ItemParamEntry.UsableOption, Class = 1 });
        harness.Define(new ItemParamEntry { Id = OtherKeyId, Type = 15, Effect = KeyBoxRules.KeyEffect, Options = ItemParamEntry.UsableOption, Class = 1 });
        foreach (var prize in new[] { NormalPrize, PrizeA, PrizeB }) harness.Define(new ItemParamEntry { Id = prize, Type = 14, Class = 1 });
        harness.Presents[NormalPresent] = Present(NormalPresent, NormalPrize);
        harness.Presents[RarePresentA] = Present(RarePresentA, PrizeA);
        harness.Presents[RarePresentB] = Present(RarePresentB, PrizeB);
        harness.Keyboxes[BoxId] = new KeyboxEntry(BoxId, [KeyId, 19840], onlyKey, NormalPresent, RarePresentA, RarePresentB, rareBRate);
        if (keyRate is { } rate) harness.KeyRates[KeyId] = rate;
        arrange?.Invoke(harness);
        return harness;
    }

    private static int[] Held(SpecialItemHarness harness) =>
        harness.Items.GetForCharacterBySlotKind(SpecialItemHarness.CharacterId, SlotKind.Inventory).Select(item => item.ItemIndex).OrderBy(id => id).ToArray();

    private static ushort CodeOf(SentFrame frame) => BitConverter.ToUInt16(frame.Payload);

    [Fact]
    public async Task TheWindowOpensForAKeyBoxInTheBag()
    {
        var harness = Harness(true, 0, 0);
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.OpenKeyBoxAsync(box);

        Assert.Equal(new[] { Opened }, harness.SentWords);
        Assert.Empty(harness.Sent[0].Payload);
        Assert.True(harness.InBag(box));
    }

    [Fact]
    public async Task TheWindowIsRefusedWithCodeOneForAnotherItemOrANoneHeld()
    {
        var harness = Harness(true, 0, 0);
        var key = harness.Give(KeyId);
        var gone = harness.Give(BoxId);
        harness.Items.Delete(gone.ItemUid);
        harness.Build();

        await harness.OpenKeyBoxAsync(key);
        await harness.OpenKeyBoxAsync(gone);

        Assert.Equal(new[] { OpenRefused, OpenRefused }, harness.SentWords);
        Assert.All(harness.Sent, frame => Assert.Equal(new byte[] { 1 }, frame.Payload));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public async Task ASafeZoneBoxOpensOnlyInTheSafeZoneTypes(int zoneType, bool opens)
    {
        var harness = Harness(true, 0, 0, h =>
        {
            h.Define(new ItemParamEntry { Id = BoxId, Type = 15, Effect = KeyBoxRules.BoxEffect, Options = ItemParamEntry.UsableOption | KeyBoxRules.SafeZoneOption, Class = 1 });
            h.State.ZoneType = zoneType;
        });
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.OpenKeyBoxAsync(box);

        Assert.Equal(new[] { opens ? Opened : OpenRefused }, harness.SentWords);
    }

    [Fact]
    public async Task AKeyTakesBoxAndKeyAndTheLowRollGivesTheFirstRarePresent()
    {
        var harness = Harness(true, 0, 0);
        var box = harness.Give(BoxId);
        var key = harness.Give(KeyId);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, key);

        Assert.Equal(new[] { Closed, Result }, harness.SentWords);
        Assert.Empty(harness.Sent[0].Payload);
        Assert.Equal(new[] { PrizeA }, Held(harness));
        var operations = QuestResultFrames.ReadItemOperations(harness.Sent[1].Payload);
        Assert.Equal(
            new[] { QuestResultFrames.Created, QuestResultFrames.Removed, QuestResultFrames.Removed },
            operations.Select(operation => operation.Kind).ToArray());
        Assert.Equal(new uint[] { PrizeA, BoxId, KeyId }, operations.Select(operation => operation.ItemId).ToArray());
    }

    [Fact]
    public async Task TheKeyRateAddsToTheRareBRateSoACertainRollGivesTheSecondRarePresent()
    {
        var harness = Harness(true, 30, 70);
        var box = harness.Give(BoxId);
        var key = harness.Give(KeyId);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, key);

        Assert.Equal(new[] { PrizeB }, Held(harness));
    }

    [Fact]
    public async Task AStackedBoxAndKeyLoseOneEachAndAnOwnedPrizeStackGrows()
    {
        var harness = Harness(true, 0, 0);
        var box = harness.Give(BoxId, 3);
        var key = harness.Give(KeyId, 2);
        var owned = harness.Give(PrizeA, 5);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, key);

        Assert.Equal(2, harness.StackOf(box));
        Assert.Equal(1, harness.StackOf(key));
        Assert.Equal(6, harness.StackOf(owned));
        var operations = QuestResultFrames.ReadItemOperations(harness.Sent[1].Payload);
        Assert.Equal(new[] { QuestResultFrames.StackChanged, QuestResultFrames.StackChanged, QuestResultFrames.StackChanged }, operations.Select(operation => operation.Kind).ToArray());
        Assert.Equal(new ushort[] { 6, 2, 1 }, operations.Select(operation => operation.Count).ToArray());
    }

    [Fact]
    public async Task ABoxWhichNeedsNoKeyGivesItsNormalPresent()
    {
        var harness = Harness(false, 0, 0);
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, null);

        Assert.Equal(new[] { Closed, Result }, harness.SentWords);
        Assert.Equal(new[] { NormalPrize }, Held(harness));
        Assert.False(harness.InBag(box));
    }

    [Fact]
    public async Task ABoxWhichNeedsAKeyRefusesWithCodeTwoAndStaysInTheBag()
    {
        var harness = Harness(true, 0, 0);
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, null);

        Assert.Equal(new[] { ConfirmRefused }, harness.SentWords);
        Assert.Equal((ushort)2, CodeOf(harness.Sent[0]));
        Assert.True(harness.InBag(box));
    }

    [Fact]
    public async Task AKeyThatIsNotListedOrNotAKeyRefusesWithCodeThree()
    {
        var harness = Harness(true, 0, 0);
        var box = harness.Give(BoxId);
        var wrongKey = harness.Give(OtherKeyId);
        var notAKey = harness.Give(PrizeA);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(box, wrongKey);
        await harness.ConfirmKeyBoxAsync(box, notAKey);

        Assert.Equal(new[] { ConfirmRefused, ConfirmRefused }, harness.SentWords);
        Assert.All(harness.Sent, frame => Assert.Equal((ushort)3, CodeOf(frame)));
        Assert.True(harness.InBag(box));
        Assert.True(harness.InBag(wrongKey));
    }

    [Fact]
    public async Task ANonBoxRefusesWithCodeOne()
    {
        var harness = Harness(true, 0, 0);
        var key = harness.Give(KeyId);
        harness.Build();

        await harness.ConfirmKeyBoxAsync(key, null);

        Assert.Equal((ushort)1, CodeOf(harness.Sent[0]));
    }

    [Fact]
    public async Task AMissingKeyRateRowMissingBoxRowOrMissingPresentRefusesWithCodeSix()
    {
        var noRate = Harness(true, 0, null);
        var noRateBox = noRate.Give(BoxId);
        var noRateKey = noRate.Give(KeyId);
        noRate.Build();
        var noRow = Harness(true, 0, 0, h => h.Keyboxes.Clear());
        var noRowBox = noRow.Give(BoxId);
        noRow.Build();
        var noPresent = Harness(true, 0, 0, h => h.Presents.Remove(RarePresentA));
        var noPresentBox = noPresent.Give(BoxId);
        var noPresentKey = noPresent.Give(KeyId);
        noPresent.Build();

        await noRate.ConfirmKeyBoxAsync(noRateBox, noRateKey);
        await noRow.ConfirmKeyBoxAsync(noRowBox, null);
        await noPresent.ConfirmKeyBoxAsync(noPresentBox, noPresentKey);

        foreach (var harness in new[] { noRate, noRow, noPresent })
        {
            Assert.Equal(new[] { ConfirmRefused }, harness.SentWords);
            Assert.Equal((ushort)6, CodeOf(harness.Sent[0]));
        }
    }

    [Fact]
    public async Task ATooHeavyRewardRefusesWithCodeFourAndAFullBagWithCodeFive()
    {
        var heavy = Harness(false, 0, 0, h => h.Define(new ItemParamEntry { Id = NormalPrize, Type = 14, Class = 1, Weight = 1000 }));
        var heavyBox = heavy.Give(BoxId);
        heavy.Build();
        var full = Harness(false, 0, 0);
        for (var filler = 0; filler < tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity - 1; filler++) full.Give(9000 + filler, 1, SlotKind.Inventory, 0);
        var fullBox = full.Give(BoxId);
        full.Build();

        await heavy.ConfirmKeyBoxAsync(heavyBox, null);
        await full.ConfirmKeyBoxAsync(fullBox, null);

        Assert.Equal((ushort)4, CodeOf(heavy.Sent[0]));
        Assert.Equal((ushort)5, CodeOf(full.Sent[0]));
        Assert.True(heavy.InBag(heavyBox));
        Assert.True(full.InBag(fullBox));
    }

    [Fact]
    public async Task ABoxIsRefusedByPlainUseBecauseTheWindowOpensIt()
    {
        var harness = Harness(false, 0, 0);
        var box = harness.Give(BoxId);
        harness.Build();

        await harness.UseAsync(box);

        Assert.Equal(new ushort[] { 0x005A }, harness.SentWords);
        Assert.True(harness.InBag(box));
    }
}
