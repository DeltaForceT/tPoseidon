namespace tPoseidon.Core.Common.Tests;

public class BuildIdentityTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void ComputeTag_IsSixteenLowercaseHexCharacters_AndDeterministic()
    {
        var first = BuildIdentity.ComputeTag("0.1.0-beta.1", Commit);
        var second = BuildIdentity.ComputeTag("0.1.0-beta.1", Commit);

        Assert.Equal(BuildIdentity.TagLength, first.Length);
        Assert.Matches("^[0-9a-f]{16}$", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeTag_ChangesWithTheVersionOrTheCommit()
    {
        var tag = BuildIdentity.ComputeTag("0.1.0-beta.1", Commit);

        Assert.NotEqual(tag, BuildIdentity.ComputeTag("0.1.0-beta.2", Commit));
        Assert.NotEqual(tag, BuildIdentity.ComputeTag("0.1.0-beta.1", "fedcba9876543210fedcba9876543210fedcba98"));
    }

    [Fact]
    public void ComputeTag_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => BuildIdentity.ComputeTag("", Commit));
        Assert.Throws<ArgumentException>(() => BuildIdentity.ComputeTag("0.1.0-beta.1", " "));
    }

    [Fact]
    public void Parse_SplitsTheCommitOffTheInformationalVersion()
    {
        var identity = BuildIdentity.Parse("0.1.0-beta.1+" + Commit, "0123456789abcdef");

        Assert.Equal("0.1.0-beta.1", identity.Version);
        Assert.Equal(Commit, identity.Commit);
        Assert.Equal("0123456", identity.ShortCommit);
        Assert.Equal("0123456789abcdef", identity.Tag);
        Assert.True(identity.IsStamped);
    }

    [Fact]
    public void Parse_WithoutMetadata_HasNoCommitAndNoTag()
    {
        var identity = BuildIdentity.Parse("0.1.0-beta.1");

        Assert.Equal("0.1.0-beta.1", identity.Version);
        Assert.Null(identity.Commit);
        Assert.Null(identity.Tag);
        Assert.False(identity.IsStamped);
        Assert.Equal("", identity.ShortCommit);
        Assert.Equal("0.1.0-beta.1", identity.ToString());
    }

    [Fact]
    public void Parse_WithAnEmptyMetadataPart_HasNoCommit()
    {
        var identity = BuildIdentity.Parse("1.2.3+");

        Assert.Equal("1.2.3", identity.Version);
        Assert.Null(identity.Commit);
    }

    [Fact]
    public void ToString_ListsTheShortCommitAndTheBuildTag()
    {
        Assert.Equal("1.2.3 (0123456)", new BuildIdentity("1.2.3", Commit, null).ToString());
        Assert.Equal("1.2.3 (build 0123456789abcdef)", new BuildIdentity("1.2.3", null, "0123456789abcdef").ToString());
        Assert.Equal("1.2.3 (0123456, build 0123456789abcdef)", new BuildIdentity("1.2.3", Commit, "0123456789abcdef").ToString());
    }

    [Fact]
    public void IsConsistent_OnlyWhenTheTagMatchesTheVersionAndCommit()
    {
        var tag = BuildIdentity.ComputeTag("1.2.3", Commit);

        Assert.True(new BuildIdentity("1.2.3", Commit, tag).IsConsistent);
        Assert.False(new BuildIdentity("1.2.4", Commit, tag).IsConsistent);
        Assert.False(new BuildIdentity("1.2.3", "fedcba9876543210fedcba9876543210fedcba98", tag).IsConsistent);
        Assert.False(new BuildIdentity("1.2.3", Commit, null).IsConsistent);
        Assert.False(new BuildIdentity("1.2.3", null, tag).IsConsistent);
    }

    [Fact]
    public void FromAssembly_ReadsAnUnstampedTestBuild()
    {
        var identity = BuildIdentity.FromAssembly(typeof(BuildIdentityTests).Assembly);

        Assert.False(string.IsNullOrEmpty(identity.Version));
        Assert.DoesNotContain('+', identity.Version);
        Assert.Null(identity.Tag);
        Assert.False(identity.IsStamped);
    }
}
