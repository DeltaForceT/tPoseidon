using tPoseidon.Core.Game.Stats;

namespace tPoseidon.Core.Game.Posture;

public enum BodyForm : byte
{
    Normal,
    Disguised,
    Other,
}

public readonly record struct PostureOutcome(bool Echo, byte? Notice);

public enum PostureState : byte
{
    Standing = 0,
    Sitting = 8,
    Lying = 0x0F,
}

public sealed class PlayerPosture
{
    public const int IdleEmoteAfterMs = 20000;
    public const int IdleEmoteMinLevel = 30;
    public const int IdleEmoteChancePercent = 40;
    public const byte IdleEmoteCode = 0;

    public const byte SittingSkillRefusal = 0x0A;
    public const byte LyingSkillRefusal = 0x09;

    public const byte FormNoticeDisguised = 0x14;
    public const byte FormNoticeOther = 0x0D;
    public const byte EmoteHappy = 3;
    public const byte EmoteSad = 4;
    public const byte EmoteAngry = 5;

    private int _idleMs;

    public PostureState State { get; private set; }

    public byte Code => State switch
    {
        PostureState.Sitting => NaturalRecovery.SitAction,
        PostureState.Lying => NaturalRecovery.LieAction,
        _ => NaturalRecovery.StandAction,
    };

    public bool SwallowsInput => State != PostureState.Standing;

    public byte? SkillRefusal => State switch
    {
        PostureState.Sitting => SittingSkillRefusal,
        PostureState.Lying => LyingSkillRefusal,
        _ => null,
    };

    public PostureOutcome Request(byte code, bool moving, bool engaged, BodyForm form)
    {
        switch (code)
        {
            case NaturalRecovery.StandAction:
                Enter(PostureState.Standing);
                return new PostureOutcome(true, null);
            case NaturalRecovery.SitAction:
                if (form != BodyForm.Normal) return new PostureOutcome(false, FormNotice(form));
                if (State == PostureState.Sitting || moving || engaged) return new PostureOutcome(false, null);
                Enter(PostureState.Sitting);
                return new PostureOutcome(true, null);
            case NaturalRecovery.LieAction:
                if (form != BodyForm.Normal) return new PostureOutcome(false, FormNoticeOther);
                Enter(PostureState.Lying);
                return new PostureOutcome(true, null);
            default:
                return new PostureOutcome(true, null);
        }
    }

    public static byte? EmoteRefusal(byte code, BodyForm form) =>
        form != BodyForm.Normal && code is EmoteHappy or EmoteSad or EmoteAngry ? FormNotice(form) : null;

    private static byte FormNotice(BodyForm form) => form == BodyForm.Disguised ? FormNoticeDisguised : FormNoticeOther;

    public void StandUp() => Enter(PostureState.Standing);

    public bool IdleEmoteDue(int elapsedMs, int level, bool moving, Func<int, int> nextBelow)
    {
        ArgumentNullException.ThrowIfNull(nextBelow);
        if (State != PostureState.Standing || moving || level < IdleEmoteMinLevel) return false;
        _idleMs += elapsedMs;
        if (_idleMs < IdleEmoteAfterMs) return false;
        _idleMs = 0;
        return nextBelow(100) < IdleEmoteChancePercent;
    }

    private void Enter(PostureState state)
    {
        if (State != state) _idleMs = 0;
        State = state;
    }
}
