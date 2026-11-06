namespace tPoseidon.Hosting;

public sealed record ClientMinimapPlan(int AddCall, int Cave, int AddEntry, int PlayerGetter);

public static class ClientMinimapPatch
{
    private const int FirstShadowNpcId = 17000;
    private const int LastShadowNpcId = 17998;
    private const int ThirdEyeOffset = 0x33DC;
    private const int PlayerGetterObjectOffset = 0x3C;
    private const int StubLength = 42;
    private const int AddCallAt = 8;

    private static readonly byte?[] AddCall = ClientCode.Pattern("52 8D 8C 37 20 02 00 00 ?? ?? ?? ?? ?? 5F 5B 5E 83 C4 7C C2 14 00");
    private static readonly byte?[] AddEntry = ClientCode.Pattern("55 8B E9 8B 85 20 0D 00 00 83 F8 28 7D 4F");
    private static readonly byte?[] PlayerGetter = ClientCode.Pattern("A1 60 2C 0F 01 8B 80 68 05 00 00 83 C0 3C C3");
    private static readonly byte?[] CavePrefix = ClientCode.Pattern("FA FF FF 83 C4 0C 5F 5E 83 C4 10 C3");

    public static ClientMinimapPlan? FindUnpatched(ReadOnlySpan<byte> executable)
    {
        var plan = Locate(executable);
        return plan is not null
            && ClientCode.IsBranch(executable, plan.AddCall, ClientCode.Call, plan.AddEntry)
            && ClientCode.IsFilled(executable, plan.Cave, StubLength, ClientCode.Padding)
                ? plan
                : null;
    }

    public static ClientMinimapPlan? FindPatched(ReadOnlySpan<byte> executable)
    {
        var plan = Locate(executable);
        return plan is not null
            && ClientCode.IsBranch(executable, plan.AddCall, ClientCode.Call, plan.Cave)
            && executable.Slice(plan.Cave, StubLength).SequenceEqual(Stub(plan))
                ? plan
                : null;
    }

    public static void Write(Span<byte> executable, ClientMinimapPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Stub(plan).CopyTo(executable.Slice(plan.Cave, StubLength));
        ClientCode.WriteBranch(executable, plan.AddCall, ClientCode.Call, plan.Cave);
    }

    public static byte[] Stub(ClientMinimapPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var code = new List<byte>(StubLength);
        code.AddRange([0x8B, 0x44, 0x24, 0x04]);
        code.AddRange([0x8B, 0x00]);
        code.Add(0x2D);
        code.AddRange(BitConverter.GetBytes(FirstShadowNpcId));
        code.Add(0x3D);
        code.AddRange(BitConverter.GetBytes(LastShadowNpcId - FirstShadowNpcId));
        var jumpToAdd = code.Count;
        code.AddRange([0x77, 0x00]);
        code.Add(0x51);
        code.Add(ClientCode.Call);
        code.AddRange(BitConverter.GetBytes(plan.PlayerGetter - (plan.Cave + code.Count + 4)));
        code.Add(0x59);
        code.AddRange([0x80, 0xB8]);
        code.AddRange(BitConverter.GetBytes(ThirdEyeOffset - PlayerGetterObjectOffset));
        code.Add(0x00);
        var jumpIfSeen = code.Count;
        code.AddRange([0x75, 0x00]);
        code.AddRange([0xC2, 0x04, 0x00]);
        var addTail = code.Count;
        code.Add(ClientCode.Jump);
        code.AddRange(BitConverter.GetBytes(plan.AddEntry - (plan.Cave + code.Count + 4)));
        code[jumpToAdd + 1] = (byte)(addTail - (jumpToAdd + 2));
        code[jumpIfSeen + 1] = (byte)(addTail - (jumpIfSeen + 2));
        return code.ToArray();
    }

    private static ClientMinimapPlan? Locate(ReadOnlySpan<byte> executable)
    {
        var call = ClientCode.FindUnique(executable, AddCall);
        var add = ClientCode.FindUnique(executable, AddEntry);
        var getter = ClientCode.FindUnique(executable, PlayerGetter);
        var prefix = ClientCode.FindUnique(executable, CavePrefix);
        if (call < 0 || add < 0 || getter < 0 || prefix < 0)
        {
            return null;
        }

        return new ClientMinimapPlan(call + AddCallAt, prefix + CavePrefix.Length, add, getter);
    }
}
