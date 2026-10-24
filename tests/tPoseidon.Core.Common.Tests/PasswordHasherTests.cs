using tPoseidon.Core.Common;

namespace tPoseidon.Core.Common.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesPhcWithExpectedPrefix()
    {
        var phc = PasswordHasher.Hash("hunter2");
        Assert.StartsWith("$argon2id$v=19$", phc);
        var parts = phc.Split('$');
        Assert.Equal(6, parts.Length);
        Assert.Equal("", parts[0]);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Contains("m=", parts[3]);
        Assert.Contains("t=", parts[3]);
        Assert.Contains("p=", parts[3]);
    }

    [Fact]
    public void Hash_IsDifferentEachTime_DueToSalt()
    {
        var a = PasswordHasher.Hash("samepw");
        var b = PasswordHasher.Hash("samepw");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        var phc = PasswordHasher.Hash("hunter2");
        Assert.True(PasswordHasher.Verify("hunter2", phc));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var phc = PasswordHasher.Hash("hunter2");
        Assert.False(PasswordHasher.Verify("hunter3", phc));
    }

    [Fact]
    public void Verify_RejectsMalformedHash()
    {
        Assert.False(PasswordHasher.Verify("anything", "not_a_phc_string"));
        Assert.False(PasswordHasher.Verify("anything", "$argon2id$v=19$truncated"));
        Assert.False(PasswordHasher.Verify("anything", "$argon2id$v=19$m=65536,t=3,p=4$bad_salt!$bad_hash"));
    }

    [Fact]
    public void Verify_RejectsBcryptOrOtherHashShape()
    {
        Assert.False(PasswordHasher.Verify("password", "$2b$12$abcdefghijklmnopqrstuvwxyz0123456789"));
    }

    [Fact]
    public void Hash_ThrowsOnEmptyOrNullPassword()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Hash(""));
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Hash(null!));
    }

    [Fact]
    public void Verify_ThrowsOnEmptyOrNullArgs()
    {
        Assert.Throws<ArgumentException>(() => PasswordHasher.Verify("", "$argon2id$v=19$m=65536,t=3,p=4$AAA$BBB"));
        Assert.Throws<ArgumentException>(() => PasswordHasher.Verify("pw", ""));
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Verify(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => PasswordHasher.Verify("x", null!));
    }
}
