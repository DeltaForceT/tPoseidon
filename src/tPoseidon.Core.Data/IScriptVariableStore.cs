namespace tPoseidon.Core.Data;

public sealed record StoredScriptVariable(string Name, long Number, string? Text);

public interface IScriptVariableStore
{
    IReadOnlyList<StoredScriptVariable> Load(byte scope, ulong ownerId);

    void Save(byte scope, ulong ownerId, StoredScriptVariable variable);
}

public sealed class InMemoryScriptVariableStore : IScriptVariableStore
{
    private readonly object _gate = new();
    private readonly Dictionary<(byte Scope, ulong Owner), Dictionary<string, StoredScriptVariable>> _owners = new();

    public IReadOnlyList<StoredScriptVariable> Load(byte scope, ulong ownerId)
    {
        lock (_gate)
        {
            return _owners.TryGetValue((scope, ownerId), out var variables) ? variables.Values.ToArray() : Array.Empty<StoredScriptVariable>();
        }
    }

    public void Save(byte scope, ulong ownerId, StoredScriptVariable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        lock (_gate)
        {
            if (!_owners.TryGetValue((scope, ownerId), out var variables))
            {
                variables = new Dictionary<string, StoredScriptVariable>(StringComparer.Ordinal);
                _owners[(scope, ownerId)] = variables;
            }
            variables[variable.Name] = variable;
        }
    }
}
