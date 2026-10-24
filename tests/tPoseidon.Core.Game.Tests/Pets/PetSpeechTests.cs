using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Pets;

public sealed class PetSpeechTests
{
    private const int FoxType = 4001;
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static PetSpeechBook Book(params (int Type, PetSpeechLine[] Lines)[] rows) => new(new PetSpeechTables(
        new Dictionary<int, PetSpeechParam> { [FoxType] = new PetSpeechParam("PetSpeech_Fox", false, 1) },
        new Dictionary<string, IReadOnlyDictionary<int, IReadOnlyList<PetSpeechLine>>>
        {
            ["PetSpeech_Fox"] = rows.ToDictionary(row => row.Type, row => (IReadOnlyList<PetSpeechLine>)row.Lines),
        }));

    private static PetSpeechLine Line(string text, int threshold) => new(text, string.Empty, threshold);

    private static int Roll(int value) => value;

    private static Func<int, int> Rolling(int value) => _ => value;

    [Fact]
    public void ASingleLineIsSpokenWithoutARoll()
    {
        var book = Book((9, [Line("Sit well", 0)]));

        Assert.Equal(new PetUtterance("Sit well", 1), book.Pick(FoxType, 9, _ => throw new InvalidOperationException()));
    }

    [Theory]
    [InlineData(0, "first")]
    [InlineData(299, "first")]
    [InlineData(300, "second")]
    [InlineData(599, "second")]
    public void SeveralLinesAreChosenByTheFirstThresholdAboveTheRoll(int roll, string expected)
    {
        var book = Book((9, [Line("first", 300), Line("second", 600)]));

        Assert.Equal(expected, book.Pick(FoxType, 9, Rolling(roll))!.Value.Text);
    }

    [Fact]
    public void ARollAboveEveryThresholdSaysNothing()
    {
        var book = Book((9, [Line("first", 300), Line("second", 600)]));

        Assert.Null(book.Pick(FoxType, 9, Rolling(600)));
    }

    [Fact]
    public void TheRollRangeIsOneThousand()
    {
        var book = Book((9, [Line("first", 300), Line("second", 600)]));
        var bound = 0;

        book.Pick(FoxType, 9, limit =>
        {
            bound = limit;
            return 0;
        });

        Assert.Equal(1000, bound);
    }

    [Fact]
    public void UnknownPetsTablesAndTypesAreSilent()
    {
        var book = Book((9, [Line("x", 0)]));

        Assert.Null(book.Pick(1, 9, Roll));
        Assert.Null(book.Pick(FoxType, 10, Roll));
        Assert.Null(PetSpeechBook.Empty.Pick(FoxType, 9, Roll));
    }

    [Fact]
    public void ARowWithoutLinesIsSilent()
    {
        var book = Book((9, []));

        Assert.Null(book.Pick(FoxType, 9, Roll));
    }

    [Fact]
    public void TheSpeechTypeIsTheEventCodeMinusOne()
    {
        Assert.Equal(9, PetSpeechBook.SpeechTypeOf(PetSpeechState.OwnerSatEvent));
        Assert.Equal(10, PetSpeechBook.SpeechTypeOf(PetSpeechState.OwnerLayDownEvent));
    }

    [Theory]
    [InlineData(NaturalRecovery.SitAction, PetSpeechState.OwnerSatEvent)]
    [InlineData(NaturalRecovery.LieAction, PetSpeechState.OwnerLayDownEvent)]
    public void SittingAndLyingRaiseTheirOwnPetEvents(byte posture, byte petEvent)
    {
        Assert.Equal(petEvent, PetSpeechState.EventForPosture(posture));
    }

    [Fact]
    public void StandingRaisesNoPetEvent()
    {
        Assert.Null(PetSpeechState.EventForPosture(NaturalRecovery.StandAction));
    }

    [Fact]
    public void ARaisedEventIsSpokenOnTheNextTick()
    {
        var book = Book((9, [Line("Sit well", 0)]));
        var state = new PetSpeechState();
        state.Tick(Start, book, FoxType, Roll);

        state.Raise(PetSpeechState.OwnerSatEvent);

        Assert.Equal(new PetUtterance("Sit well", 1), state.Tick(Start.AddMilliseconds(100), book, FoxType, Roll));
        Assert.Null(state.Tick(Start.AddMilliseconds(200), book, FoxType, Roll));
    }

    [Fact]
    public void ASecondEventDuringTheCooldownIsDropped()
    {
        var book = Book((9, [Line("Sit well", 0)]), (10, [Line("Nap time", 0)]));
        var state = new PetSpeechState();
        state.Tick(Start, book, FoxType, Roll);
        state.Raise(PetSpeechState.OwnerSatEvent);
        state.Tick(Start.AddMilliseconds(100), book, FoxType, Roll);

        state.Raise(PetSpeechState.OwnerLayDownEvent);

        Assert.Null(state.Tick(Start.AddMilliseconds(1000), book, FoxType, Roll));
    }

    [Fact]
    public void TheCooldownEndsAfterThreeSecondsForOrdinaryEvents()
    {
        var book = Book((9, [Line("Sit well", 0)]), (10, [Line("Nap time", 0)]));
        var state = new PetSpeechState();
        state.Tick(Start, book, FoxType, Roll);
        state.Raise(PetSpeechState.OwnerSatEvent);
        state.Tick(Start, book, FoxType, Roll);

        state.Tick(Start.AddMilliseconds(3001), book, FoxType, Roll);
        state.Raise(PetSpeechState.OwnerLayDownEvent);

        Assert.Equal("Nap time", state.Tick(Start.AddMilliseconds(3100), book, FoxType, Roll)!.Value.Text);
    }

    [Fact]
    public void AnEventWithoutALineIsDiscardedWithoutACooldown()
    {
        var book = Book((9, [Line("Sit well", 0)]));
        var state = new PetSpeechState();
        state.Tick(Start, book, FoxType, Roll);
        state.Raise(PetSpeechState.OwnerLayDownEvent);

        Assert.Null(state.Tick(Start.AddMilliseconds(100), book, FoxType, Roll));
        state.Raise(PetSpeechState.OwnerSatEvent);
        Assert.NotNull(state.Tick(Start.AddMilliseconds(200), book, FoxType, Roll));
    }

    [Fact]
    public void TheOwnerDyingClearsAPendingEventAndIsItselfRaised()
    {
        var book = Book((9, [Line("Sit well", 0)]), (2, [Line("Get up", 0)]));
        var state = new PetSpeechState();
        state.Tick(Start, book, FoxType, Roll);
        state.Raise(PetSpeechState.OwnerSatEvent);

        state.Raise(PetSpeechState.OwnerDiedEvent);

        Assert.Equal("Get up", state.Tick(Start.AddMilliseconds(100), book, FoxType, Roll)!.Value.Text);
    }
}
