namespace tPoseidon.Core.Common.Tests;

public sealed class OpcodesTests
{
    [Fact]
    public void Opcode_Enum_Underlying_Is_UShort()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(Opcode)));
    }

    [Theory]
    [InlineData(Opcode.LOGIN_RESPONSE, 0x0321)]
    [InlineData(Opcode.HEARTBEAT_A, 0x076C)]
    [InlineData(Opcode.HEARTBEAT_ACK, 0x07EC)]
    [InlineData(Opcode.SESSION_OPEN, 0x1582)]
    [InlineData(Opcode.S2C_OP_177D_TBD, 0x177D)]
    [InlineData(Opcode.CHAR_INFO, 0x1B5A)]
    [InlineData(Opcode.S2C_OP_213D_TBD, 0x213D)]
    [InlineData(Opcode.S2C_OP_2711_TBD, 0x2711)]
    [InlineData(Opcode.S2C_OP_2BFD_TBD, 0x2BFD)]
    [InlineData(Opcode.S2C_OP_2EAF_TBD, 0x2EAF)]
    [InlineData(Opcode.SERVER_BROADCAST, 0xFDEB)]
    public void Known_Opcodes_Match_Tier1_Table(Opcode opcode, ushort expected)
    {
        Assert.Equal(expected, (ushort)opcode);
    }

    [Fact]
    public void Opcode_Has_No_Duplicate_Values()
    {
        var values = Enum.GetValues<Opcode>();
        var distinct = new HashSet<ushort>(values.Select(v => (ushort)v));
        Assert.Equal(values.Length, distinct.Count);
    }

    [Fact]
    public void Opcode_Has_Reasonable_Count()
    {
        var count = Enum.GetValues<Opcode>().Length;
        Assert.True(count > 200, $"Expected >200 opcodes catalogued; got {count}");
    }
}
