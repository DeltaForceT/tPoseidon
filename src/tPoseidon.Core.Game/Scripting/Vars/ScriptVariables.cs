using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.Game.Scripting.Vars;

public sealed class ScriptVariables
{
    public const int MaximumNameLength = 64;
    public const int MaximumVariablesPerOwner = 512;
    public const ulong ServerOwner = 0;

    private readonly IScriptVariableStore _store;
    private readonly object _gate = new();
    private readonly Dictionary<(VariableScope Scope, ulong Owner), Dictionary<string, ScriptValue>> _cache = new();

    public ScriptVariables(IScriptVariableStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public ScriptValue Get(VariableScope scope, ulong ownerId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return OwnerCache(scope, ownerId).TryGetValue(name, out var value) ? value : ScriptValue.Zero;
        }
    }

    public void Set(VariableScope scope, ulong ownerId, string name, ScriptValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || name.Length > MaximumNameLength)
        {
            throw new ScriptRuntimeException($"A variable name must be 1 to {MaximumNameLength} characters long");
        }
        lock (_gate)
        {
            var owned = OwnerCache(scope, ownerId);
            if (!owned.ContainsKey(name) && owned.Count >= MaximumVariablesPerOwner)
            {
                throw new ScriptRuntimeException($"At most {MaximumVariablesPerOwner} variables can be kept per {Describe(scope)}");
            }
            owned[name] = value;
            _store.Save((byte)scope, ownerId, new StoredScriptVariable(name, value.IsText ? 0 : value.Number, value.IsText ? value.Text : null));
        }
    }

    private Dictionary<string, ScriptValue> OwnerCache(VariableScope scope, ulong ownerId)
    {
        if (_cache.TryGetValue((scope, ownerId), out var existing)) return existing;
        var loaded = new Dictionary<string, ScriptValue>(StringComparer.Ordinal);
        foreach (var variable in _store.Load((byte)scope, ownerId))
        {
            loaded[variable.Name] = variable.Text is null ? ScriptValue.FromNumber(variable.Number) : ScriptValue.FromText(variable.Text);
        }
        _cache[(scope, ownerId)] = loaded;
        return loaded;
    }

    private static string Describe(VariableScope scope) => scope switch
    {
        VariableScope.Character => "character",
        VariableScope.Account => "account",
        _ => "server",
    };
}
