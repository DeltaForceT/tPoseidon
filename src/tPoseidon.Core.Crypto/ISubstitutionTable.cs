namespace tPoseidon.Core.Crypto;

public interface ISubstitutionTable
{
        int Length { get; }

        byte Substitute(byte keyNibble, byte value);
}
