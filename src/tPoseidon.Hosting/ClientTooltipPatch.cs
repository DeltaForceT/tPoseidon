namespace tPoseidon.Hosting;

public sealed record ClientTooltipPlan(int[] StatsCalls, int[] ElementCalls, int Stats, int Element, int Cave);

public static class ClientTooltipPatch
{
    private const int StubLength = 43;
    private const int StatsCallAt = 8;
    private const int ElementCallAt = 15;
    private const int ArgumentCount = 5;

    private static readonly byte[] SkippedCall = [0x83, 0xC4, 0x14, ClientCode.Nop, ClientCode.Nop];

    private static readonly byte?[][] StatsCalls =
    [
        ClientCode.Pattern("51 8D 54 24 24 52 8B CE ?? ?? ?? ?? ?? 8D 4C 24 24 E8 ?? ?? ?? ?? 8B 4C 24 60"),
        ClientCode.Pattern("51 8D 54 24 24 52 8B CE ?? ?? ?? ?? ?? 8D 4C 24 2C E8 ?? ?? ?? ?? 8B 4C 24 68"),
    ];

    private static readonly byte?[][] ElementCalls =
    [
        ClientCode.Pattern("8D 54 24 10 52 55 57 53 8D 44 24 24 50 8B CE ?? ?? ?? ?? ?? 8D 4C 24 10 51 55 57 53 8D 54 24 24 52 68 C4 5D 00 00 68 C2 5D 00 00"),
        ClientCode.Pattern("8D 54 24 10 52 53 55 57 8D 44 24 24 50 8B CE ?? ?? ?? ?? ?? 8D 4C 24 10 51 53 55 57 8D 54 24 24 52 68 C4 5D 00 00 68 C2 5D 00 00"),
    ];

    private static readonly byte?[] Stats = ClientCode.Pattern("6A FF 68 40 F7 D7 00 64 A1 00 00 00 00 50 64 89 25 00 00 00 00 83 EC 14 53 55 56 57 89 4C");
    private static readonly byte?[] Element = ClientCode.Pattern("6A FF 68 00 F8 D7 00 64 A1 00 00 00 00 50 64 89 25 00 00 00 00 83 EC 14 53 55 56 57 8B E9");
    private static readonly byte?[] CavePrefix = ClientCode.Pattern("E8 98 F7 FF FF 5F B0 01 5E C2 04 00");

    public static ClientTooltipPlan? FindUnpatched(ReadOnlySpan<byte> executable)
    {
        var plan = Locate(executable);
        if (plan is null || !ClientCode.IsFilled(executable, plan.Cave, StubLength, ClientCode.Padding))
        {
            return null;
        }

        foreach (var call in plan.StatsCalls)
        {
            if (!ClientCode.IsBranch(executable, call, ClientCode.Call, plan.Stats))
            {
                return null;
            }
        }

        foreach (var call in plan.ElementCalls)
        {
            if (!ClientCode.IsBranch(executable, call, ClientCode.Call, plan.Element))
            {
                return null;
            }
        }

        return plan;
    }

    public static ClientTooltipPlan? FindPatched(ReadOnlySpan<byte> executable)
    {
        var plan = Locate(executable);
        if (plan is null || !executable.Slice(plan.Cave, StubLength).SequenceEqual(Stub(plan)))
        {
            return null;
        }

        foreach (var call in plan.StatsCalls)
        {
            if (!ClientCode.IsBranch(executable, call, ClientCode.Call, plan.Cave))
            {
                return null;
            }
        }

        foreach (var call in plan.ElementCalls)
        {
            if (!executable.Slice(call, SkippedCall.Length).SequenceEqual(SkippedCall))
            {
                return null;
            }
        }

        return plan;
    }

    public static void Write(Span<byte> executable, ClientTooltipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Stub(plan).CopyTo(executable.Slice(plan.Cave, StubLength));
        foreach (var call in plan.StatsCalls)
        {
            ClientCode.WriteBranch(executable, call, ClientCode.Call, plan.Cave);
        }

        foreach (var call in plan.ElementCalls)
        {
            SkippedCall.CopyTo(executable.Slice(call, SkippedCall.Length));
        }
    }

    public static byte[] Stub(ClientTooltipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var code = new List<byte>(StubLength) { 0x51 };
        for (var argument = 0; argument < ArgumentCount; argument++)
        {
            code.AddRange([0xFF, 0x74, 0x24, 0x18]);
        }

        code.Add(ClientCode.Call);
        code.AddRange(BitConverter.GetBytes(plan.Stats - (plan.Cave + code.Count + 4)));
        code.Add(0x59);
        code.AddRange([0x8B, 0x44, 0x24, 0x08]);
        code.AddRange([0x8B, 0x40, 0xF4]);
        code.AddRange([0x89, 0x44, 0x24, 0x08]);
        code.Add(ClientCode.Jump);
        code.AddRange(BitConverter.GetBytes(plan.Element - (plan.Cave + code.Count + 4)));
        return code.ToArray();
    }

    private static ClientTooltipPlan? Locate(ReadOnlySpan<byte> executable)
    {
        var stats = ClientCode.FindUnique(executable, Stats);
        var element = ClientCode.FindUnique(executable, Element);
        var prefix = ClientCode.FindUnique(executable, CavePrefix);
        if (stats < 0 || element < 0 || prefix < 0)
        {
            return null;
        }

        var statsCalls = new int[StatsCalls.Length];
        for (var i = 0; i < statsCalls.Length; i++)
        {
            var window = ClientCode.FindUnique(executable, StatsCalls[i]);
            if (window < 0)
            {
                return null;
            }

            statsCalls[i] = window + StatsCallAt;
        }

        var elementCalls = new int[ElementCalls.Length];
        for (var i = 0; i < elementCalls.Length; i++)
        {
            var window = ClientCode.FindUnique(executable, ElementCalls[i]);
            if (window < 0)
            {
                return null;
            }

            elementCalls[i] = window + ElementCallAt;
        }

        return new ClientTooltipPlan(statsCalls, elementCalls, stats, element, prefix + CavePrefix.Length);
    }
}
