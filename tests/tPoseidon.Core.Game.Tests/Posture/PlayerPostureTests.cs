using tPoseidon.Core.Game.Posture;
using tPoseidon.Core.Game.Stats;

namespace tPoseidon.Core.Game.Tests.Posture;

public sealed class PlayerPostureTests
{
    private static int NeverRolled(int bound) => bound - 1;

    private static int AlwaysRolled(int bound) => 0;

    [Fact]
    public void ANewPostureIsStandingWithTheStandCode()
    {
        var posture = new PlayerPosture();

        Assert.Equal(PostureState.Standing, posture.State);
        Assert.Equal(NaturalRecovery.StandAction, posture.Code);
        Assert.False(posture.SwallowsInput);
        Assert.Null(posture.SkillRefusal);
    }

    [Fact]
    public void SittingWhileIdleIsAcceptedAndShowsTheSitCode()
    {
        var posture = new PlayerPosture();

        Assert.True(posture.Request(NaturalRecovery.SitAction, moving: false, engaged: false, BodyForm.Normal).Echo);

        Assert.Equal(PostureState.Sitting, posture.State);
        Assert.Equal(NaturalRecovery.SitAction, posture.Code);
        Assert.True(posture.SwallowsInput);
        Assert.Equal(PlayerPosture.SittingSkillRefusal, posture.SkillRefusal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void SittingIsRefusedWhileWalkingOrFighting(bool moving, bool engaged)
    {
        var posture = new PlayerPosture();

        Assert.False(posture.Request(NaturalRecovery.SitAction, moving, engaged, BodyForm.Normal).Echo);

        Assert.Equal(PostureState.Standing, posture.State);
    }

    [Fact]
    public void SittingAgainWhileSittingIsRefused()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);

        Assert.False(posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal).Echo);
    }

    [Fact]
    public void LyingDownIsAcceptedFromStandingAndFromSitting()
    {
        var posture = new PlayerPosture();

        Assert.True(posture.Request(NaturalRecovery.LieAction, false, false, BodyForm.Normal).Echo);
        Assert.Equal(PostureState.Lying, posture.State);
        Assert.Equal(PlayerPosture.LyingSkillRefusal, posture.SkillRefusal);

        posture.Request(NaturalRecovery.StandAction, false, false, BodyForm.Normal);
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);
        Assert.True(posture.Request(NaturalRecovery.LieAction, true, true, BodyForm.Normal).Echo);
        Assert.Equal(PostureState.Lying, posture.State);
    }

    [Fact]
    public void LyingPlayersCanSitUp()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.LieAction, false, false, BodyForm.Normal);

        Assert.True(posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal).Echo);
        Assert.Equal(PostureState.Sitting, posture.State);
    }

    [Fact]
    public void StandingUpIsAlwaysAccepted()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);

        Assert.True(posture.Request(NaturalRecovery.StandAction, true, true, BodyForm.Normal).Echo);
        Assert.Equal(PostureState.Standing, posture.State);
        Assert.False(posture.SwallowsInput);
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)4)]
    [InlineData((byte)5)]
    [InlineData((byte)6)]
    [InlineData((byte)0x13)]
    public void OtherCodesAreRelayedWithoutChangingThePosture(byte code)
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);

        Assert.True(posture.Request(code, false, false, BodyForm.Normal).Echo);
        Assert.Equal(PostureState.Sitting, posture.State);
    }

    [Fact]
    public void StandUpEndsAnyPosture()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.LieAction, false, false, BodyForm.Normal);

        posture.StandUp();

        Assert.Equal(PostureState.Standing, posture.State);
        Assert.Equal(NaturalRecovery.StandAction, posture.Code);
    }

    [Fact]
    public void TheIdleEmoteComesAfterTwentySecondsOfStandingWhenTheRollPasses()
    {
        var posture = new PlayerPosture();

        Assert.False(posture.IdleEmoteDue(19999, 30, false, AlwaysRolled));
        Assert.True(posture.IdleEmoteDue(1, 30, false, AlwaysRolled));
    }

    [Fact]
    public void TheIdleEmoteRollPassesBelowFortyPercent()
    {
        var posture = new PlayerPosture();

        Assert.True(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs, 30, false, bound => bound == 100 ? 39 : -1));
        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs, 30, false, bound => bound == 100 ? 40 : -1));
    }

    [Fact]
    public void AFailedRollStillRestartsTheTwentySeconds()
    {
        var posture = new PlayerPosture();

        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs, 30, false, NeverRolled));
        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs - 1, 30, false, AlwaysRolled));
        Assert.True(posture.IdleEmoteDue(1, 30, false, AlwaysRolled));
    }

    [Fact]
    public void PlayersBelowLevelThirtyNeverIdleEmote()
    {
        var posture = new PlayerPosture();

        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs * 3, 29, false, AlwaysRolled));
    }

    [Fact]
    public void WalkingPlayersDoNotAccumulateIdleTime()
    {
        var posture = new PlayerPosture();

        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs, 50, true, AlwaysRolled));
        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs - 1, 50, false, AlwaysRolled));
    }

    [Fact]
    public void SittingPlayersNeverIdleEmote()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);

        Assert.False(posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs * 2, 50, false, AlwaysRolled));
    }

    [Fact]
    public void ChangingPostureRestartsTheIdleTime()
    {
        var posture = new PlayerPosture();
        posture.IdleEmoteDue(PlayerPosture.IdleEmoteAfterMs - 1, 50, false, AlwaysRolled);
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);
        posture.Request(NaturalRecovery.StandAction, false, false, BodyForm.Normal);

        Assert.False(posture.IdleEmoteDue(1, 50, false, AlwaysRolled));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RestingRecoversMoreThanStandingAndLyingMoreThanSitting(int characterType)
    {
        var posture = new PlayerPosture();
        var standing = NaturalRecovery.AmountsFor(characterType, posture.Code)!.Value;
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);
        var sitting = NaturalRecovery.AmountsFor(characterType, posture.Code)!.Value;
        posture.Request(NaturalRecovery.LieAction, false, false, BodyForm.Normal);
        var lying = NaturalRecovery.AmountsFor(characterType, posture.Code)!.Value;

        Assert.Equal(new RecoveryAmounts(5, 5), standing);
        Assert.True(sitting.Hp > standing.Hp && sitting.Mp > standing.Mp);
        Assert.True(lying.Hp > sitting.Hp && lying.Mp > sitting.Mp);
    }

    [Fact]
    public void ASittingPlayerGainsFifteenHpAndTenMpOnTheNextTick()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);
        var recovery = new NaturalRecovery();
        recovery.Tick(0, 100, 500, 100, 500, NaturalRecovery.AmountsFor(0, NaturalRecovery.StandAction)!.Value);

        var report = recovery.Tick(NaturalRecovery.PeriodMs, 105, 500, 105, 500, NaturalRecovery.AmountsFor(0, posture.Code)!.Value);

        Assert.Equal(new RecoveryReport(120, 115), report);
    }

    [Fact]
    public void AStandingPlayerGainsFiveHpAndFiveMpOnTheNextTick()
    {
        var posture = new PlayerPosture();
        var recovery = new NaturalRecovery();
        recovery.Tick(0, 100, 500, 100, 500, NaturalRecovery.AmountsFor(0, posture.Code)!.Value);

        var report = recovery.Tick(NaturalRecovery.PeriodMs, 105, 500, 105, 500, NaturalRecovery.AmountsFor(0, posture.Code)!.Value);

        Assert.Equal(new RecoveryReport(110, 110), report);
    }

    [Theory]
    [InlineData(BodyForm.Disguised, PlayerPosture.FormNoticeDisguised)]
    [InlineData(BodyForm.Other, PlayerPosture.FormNoticeOther)]
    public void SittingIsRefusedWithAFormNoticeWhenNotInTheNormalForm(BodyForm form, byte notice)
    {
        var posture = new PlayerPosture();

        var outcome = posture.Request(NaturalRecovery.SitAction, false, false, form);

        Assert.Equal(new PostureOutcome(false, notice), outcome);
        Assert.Equal(PostureState.Standing, posture.State);
    }

    [Theory]
    [InlineData(BodyForm.Disguised)]
    [InlineData(BodyForm.Other)]
    public void LyingIsRefusedWithTheOtherFormNoticeWhenNotInTheNormalForm(BodyForm form)
    {
        var posture = new PlayerPosture();

        var outcome = posture.Request(NaturalRecovery.LieAction, false, false, form);

        Assert.Equal(new PostureOutcome(false, PlayerPosture.FormNoticeOther), outcome);
        Assert.Equal(PostureState.Standing, posture.State);
    }

    [Fact]
    public void StandingUpWorksInAnyForm()
    {
        var posture = new PlayerPosture();
        posture.Request(NaturalRecovery.SitAction, false, false, BodyForm.Normal);

        Assert.Equal(new PostureOutcome(true, null), posture.Request(NaturalRecovery.StandAction, false, false, BodyForm.Disguised));
        Assert.Equal(PostureState.Standing, posture.State);
    }

    [Fact]
    public void ARefusedSitDoesNotAffectTheMoveAndFightChecksOfTheNormalForm()
    {
        var posture = new PlayerPosture();

        Assert.Equal(new PostureOutcome(false, null), posture.Request(NaturalRecovery.SitAction, true, false, BodyForm.Normal));
    }

    [Theory]
    [InlineData((byte)3, BodyForm.Disguised, PlayerPosture.FormNoticeDisguised)]
    [InlineData((byte)4, BodyForm.Other, PlayerPosture.FormNoticeOther)]
    [InlineData((byte)5, BodyForm.Disguised, PlayerPosture.FormNoticeDisguised)]
    public void TheThreeFaceEmotesAreRefusedWithAFormNoticeOutsideTheNormalForm(byte code, BodyForm form, byte notice)
    {
        Assert.Equal(notice, PlayerPosture.EmoteRefusal(code, form));
    }

    [Theory]
    [InlineData((byte)3)]
    [InlineData((byte)6)]
    public void FaceEmotesPassInTheNormalForm(byte code)
    {
        Assert.Null(PlayerPosture.EmoteRefusal(code, BodyForm.Normal));
    }

    [Theory]
    [InlineData(BodyForm.Disguised)]
    [InlineData(BodyForm.Other)]
    public void TheKiEmoteAndOtherCodesAreNeverRefused(BodyForm form)
    {
        Assert.Null(PlayerPosture.EmoteRefusal(6, form));
        Assert.Null(PlayerPosture.EmoteRefusal(0x13, form));
    }
}
