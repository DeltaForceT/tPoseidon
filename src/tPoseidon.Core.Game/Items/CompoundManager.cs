using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class CompoundManager
{
    private readonly Dictionary<int, List<CompoundRecipeEntry>> _recipesById;
    private readonly Dictionary<(string Table, int Id), CompoundRecipeEntry> _recipesByKey;
    private readonly int _recipeCount;
    private readonly Dictionary<string, List<CompounderShopEntry>> _shopsByMap;

    public CompoundManager(
        IEnumerable<CompoundRecipeEntry> recipes,
        IEnumerable<CompounderShopEntry> shops)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(shops);
        var list = recipes.ToList();
        _recipeCount = list.Count;
        _recipesById = new Dictionary<int, List<CompoundRecipeEntry>>();
        _recipesByKey = new Dictionary<(string, int), CompoundRecipeEntry>();
        foreach (var r in list)
        {
            if (!_recipesById.TryGetValue(r.CompoundId, out var bucket))
            {
                bucket = new List<CompoundRecipeEntry>();
                _recipesById[r.CompoundId] = bucket;
            }
            bucket.Add(r);
            _recipesByKey[(r.TableName, r.CompoundId)] = r;
        }
        _shopsByMap = new Dictionary<string, List<CompounderShopEntry>>(StringComparer.Ordinal);
        foreach (var s in shops)
        {
            if (!_shopsByMap.TryGetValue(s.MapName, out var bucket))
            {
                bucket = new List<CompounderShopEntry>();
                _shopsByMap[s.MapName] = bucket;
            }
            bucket.Add(s);
        }
    }

    public int RecipeCount => _recipeCount;

    public IReadOnlyList<CompoundRecipeEntry> GetRecipes(int compoundId)
        => _recipesById.TryGetValue(compoundId, out var v) ? v : (IReadOnlyList<CompoundRecipeEntry>)Array.Empty<CompoundRecipeEntry>();

    public CompoundRecipeEntry? GetRecipe(string tableName, int compoundId)
        => _recipesByKey.TryGetValue((tableName, compoundId), out var v) ? v : null;

    public IReadOnlyList<CompounderShopEntry> GetShopsForMap(string mapName)
        => _shopsByMap.TryGetValue(mapName, out var v) ? v : (IReadOnlyList<CompounderShopEntry>)Array.Empty<CompounderShopEntry>();
}
