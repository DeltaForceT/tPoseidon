using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public sealed class CharacterCombatInfoTests
{
    private static readonly Encoding Korean;
    private static readonly int[] TwoAttackFrames = { 100, 150 };
    private static readonly int[] OneReadyFrame = { 240 };
    private static readonly int[] OneAttackFrame = { 600 };

    static CharacterCombatInfoTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Korean = Encoding.GetEncoding(NoriAnimationReader.NameCodePage);
    }

    private static byte[] SpriteFile(params (string Name, int[] FrameTimes)[] animations)
    {
        var bytes = new List<byte>();
        void Int(int value)
        {
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            bytes.AddRange(buffer.ToArray());
        }
        void Zero(int count) => bytes.AddRange(new byte[count]);

        bytes.AddRange("NORI"u8.ToArray());
        Int(303);
        Zero(20);
        Int(animations.Length);
        Zero(8);

        bytes.AddRange("GAWI"u8.ToArray());
        Int(300);
        Int(16);
        Int(0);
        Int(0);
        Zero(16);
        Int(1);
        Zero(4);

        Int(0);
        Int(1);
        Int(4);
        Zero(20);
        Zero(4);

        var bodies = new List<byte[]>();
        foreach (var (name, frameTimes) in animations)
        {
            var body = new List<byte>();
            var nameBytes = new byte[32];
            Korean.GetBytes(name).CopyTo(nameBytes, 0);
            body.AddRange(nameBytes);
            body.AddRange(BitConverter.GetBytes(frameTimes.Length));
            for (var i = 0; i < frameTimes.Length; i++) body.AddRange(BitConverter.GetBytes(i * 8));
            foreach (var time in frameTimes)
            {
                body.AddRange(BitConverter.GetBytes(time));
                body.AddRange(new byte[4]);
            }
            bodies.Add(body.ToArray());
        }
        var offset = 0;
        foreach (var body in bodies)
        {
            Int(offset);
            offset += body.Length;
        }
        foreach (var body in bodies) bytes.AddRange(body);
        return bytes.ToArray();
    }

    [Fact]
    public void Reader_SumsFrameTimesPerAnimation()
    {
        var file = SpriteFile(
            (CharacterCombatInfo.AttackAnimation + "_a", TwoAttackFrames),
            (CharacterCombatInfo.AttackReadyAnimation, OneReadyFrame));

        var animations = NoriAnimationReader.Read(file, Korean);

        Assert.Equal(2, animations.Count);
        Assert.Equal(250, NoriAnimationReader.TotalOf(animations, CharacterCombatInfo.AttackAnimation));
        Assert.Equal(240, NoriAnimationReader.TotalOf(animations, CharacterCombatInfo.AttackReadyAnimation));
        Assert.Null(NoriAnimationReader.TotalOf(animations, CharacterCombatInfo.Attack2Animation));
    }

    [Fact]
    public void MissingAnimationsDefaultToOneSecondAndAMissingFileToZero()
    {
        var entry = new CharacterInfoEntry { Id = 1, MinAtkRange = 50, MaxAtkRange = 70, ChaseRange = 300 };
        var animations = NoriAnimationReader.Read(
            SpriteFile((CharacterCombatInfo.AttackAnimation, OneAttackFrame)), Korean);

        var loaded = CharacterCombatInfo.From(entry, animations);
        var missing = CharacterCombatInfo.From(entry, null);

        Assert.Equal((600, 1000, 1000), (loaded.AttackMs, loaded.Attack2Ms, loaded.AttackReadyMs));
        Assert.Equal((0, 0, 0), (missing.AttackMs, missing.Attack2Ms, missing.AttackReadyMs));
    }

    [Theory]
    [InlineData(0, 0, 0, 71, 86, 200)]
    [InlineData(60, 86, 216, 60, 86, 216)]
    [InlineData(90, 90, 300, 75, 90, 300)]
    public void RangeDefaultsFollowTheServerRules(int min, int max, int chase, int expectedMin, int expectedMax, int expectedChase)
    {
        var info = CharacterCombatInfo.From(
            new CharacterInfoEntry { Id = 1, MinAtkRange = min, MaxAtkRange = max, ChaseRange = chase }, null);

        Assert.Equal((expectedMin, expectedMax, expectedChase), (info.MinAttackRange, info.MaxAttackRange, info.ChaseRange));
        Assert.Equal((expectedMin + expectedMax) / 2, info.ApproachRadius);
    }

    [Fact]
    public void SwingDelaysFollowTheServerFormulas()
    {
        var info = new CharacterCombatInfo
        {
            Id = 1,
            MinAttackRange = 50,
            MaxAttackRange = 70,
            ChaseRange = 300,
            WalkSpeed = 0,
            RunSpeed = 0,
            AttackMs = 600,
            Attack2Ms = 1050,
            AttackReadyMs = 240,
        };

        Assert.Equal(1840, info.PlayerSwingDelayMs(10));
        Assert.Equal(756, info.PlayerSwingDelayMs(-10));
        Assert.Equal(588, info.PlayerSwingDelayMs(-50));
        Assert.Equal(2050, info.PlayerCriticalSwingDelayMs(10));
        Assert.Equal(1600, info.MonsterSwingDelayMs(10));
        Assert.Equal(600, info.MonsterSwingDelayMs(-5));
    }
}
