namespace tPoseidon.Core.Common;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class WireOpcodeAttribute : Attribute
{
    public WireOpcodeAttribute(ushort opcode) => Opcode = opcode;

    public ushort Opcode { get; }
}
