namespace tPoseidon.Core.LibConfig;

public interface INpcActionScriptLookup
{
    NpcActionScriptEntry? GetByType(int type);

    int Count { get; }
}
