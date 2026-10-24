using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class QuestResultFramesTests
{
    [Fact]
    public void TheResultListsBagChangesThenRewardsThenTheNumbers()
    {
        var payload = QuestResultFrames.Result(
            new[]
            {
                new BagOperation(QuestResultFrames.Created, 1, 0x022F, 0x0000000100000007, 1),
                new BagOperation(QuestResultFrames.Removed, 1, 0x2E84, 0x0000000100000005, 0),
            },
            new[] { ((uint)0x022F, (ushort)1) },
            exp: 1374,
            tmExp: 1374,
            questId: 1234,
            resultRowCount: 1,
            gelder: 2000);

        Assert.Equal(
            new byte[]
            {
                0x02, 0x00,
                0x04, 0x01, 0x2F, 0x02, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x03, 0x01, 0x84, 0x2E, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x01, 0x00,
                0x2F, 0x02, 0x00, 0x00, 0x01, 0x00,
                0x5E, 0x05, 0x00, 0x00,
                0x5E, 0x05, 0x00, 0x00,
                0xD2, 0x04, 0x00, 0x00,
                0x01, 0x00,
                0xD0, 0x07, 0x00, 0x00,
            },
            payload);
    }

    [Fact]
    public void AStackThatShrinksCarriesWhatIsLeft()
    {
        var payload = QuestResultFrames.Result(
            new[] { new BagOperation(QuestResultFrames.StackChanged, 1, 0x2E84, 0x0000000100000005, 3) },
            Array.Empty<(uint ItemId, ushort Count)>(),
            0, 0, 1234, 1, 0);

        Assert.Equal(new byte[] { 0x01, 0x00, 0x01, 0x01 }, payload[..4]);
        Assert.Equal(new byte[] { 0x03, 0x00 }, payload[16..18]);
        Assert.Equal(2 + 16 + 2 + 18, payload.Length);
    }

    [Fact]
    public void ACreatedRareItemCarriesItsFourBytesAndAnEmptyOptionList()
    {
        var created = QuestResultFrames.Result(
            new[] { new BagOperation(QuestResultFrames.Created, 2, 0x1000, 0x0000000100000009, 1) },
            Array.Empty<(uint ItemId, ushort Count)>(),
            0, 0, 1, 1, 0);
        var removed = QuestResultFrames.Result(
            new[] { new BagOperation(QuestResultFrames.Removed, 2, 0x1000, 0x0000000100000009, 0) },
            Array.Empty<(uint ItemId, ushort Count)>(),
            0, 0, 1, 1, 0);

        Assert.Equal(2 + 21 + 2 + 18, created.Length);
        Assert.Equal(2 + 20 + 2 + 18, removed.Length);
    }

    [Fact]
    public void TheNormalQuestResultEndsWithTheShortIdTheCountTheMoneyAndTheSkill()
    {
        var payload = QuestResultFrames.QuestResult(
            new[] { new BagOperation(QuestResultFrames.Created, 1, 0x022F, 0x0000000100000007, 1) },
            new[] { ((uint)0x022F, (ushort)1) },
            exp: 300,
            tmExp: 200,
            questId: 0x2329,
            count: 1,
            gelder: 500,
            rewardSkill: 0,
            rewardSkillLevel: 0);

        Assert.Equal(
            new byte[]
            {
                0x01, 0x00,
                0x04, 0x01, 0x2F, 0x02, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
                0x01, 0x00,
                0x2F, 0x02, 0x00, 0x00, 0x01, 0x00,
                0x2C, 0x01, 0x00, 0x00,
                0xC8, 0x00, 0x00, 0x00,
                0x29, 0x23,
                0x01, 0x00,
                0xF4, 0x01, 0x00, 0x00,
                0x00, 0x00,
                0x00, 0x00,
            },
            payload);
    }

    [Fact]
    public void AnItemFromAnNpcIsOneCreatedOperation()
    {
        Assert.Equal(
            new byte[]
            {
                0x01, 0x00,
                0x04, 0x01, 0x1D, 0x2E, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00,
            },
            QuestResultFrames.ItemOperations(new[] { new BagOperation(QuestResultFrames.Created, 1, 0x2E1D, 0x0000000100000007, 1) }));
    }

    [Fact]
    public void TheMonsterQuestResultEndsWithTheShortIdAndTheMoney()
    {
        var payload = QuestResultFrames.MonsterQuestResult(
            Array.Empty<BagOperation>(),
            new[] { ((uint)0x0068, (ushort)5) },
            exp: 300,
            tmExp: 200,
            questId: 0x0208,
            gelder: 0);

        Assert.Equal(
            new byte[]
            {
                0x00, 0x00,
                0x01, 0x00,
                0x68, 0x00, 0x00, 0x00, 0x05, 0x00,
                0x2C, 0x01, 0x00, 0x00,
                0xC8, 0x00, 0x00, 0x00,
                0x08, 0x02,
                0x00, 0x00, 0x00, 0x00,
            },
            payload);
    }
}
