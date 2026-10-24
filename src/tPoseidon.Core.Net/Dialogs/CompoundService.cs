using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record CompoundPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> Da { get; init; }
}

public sealed class CompoundService
{
    private sealed record PendingCompound(int RecipeId, int Count, IReadOnlyList<CompoundMaterial> Materials, CompoundRoll Roll);

    private readonly S2SessionContext _ctx;
    private readonly MaterialExchange _exchange;
    private readonly IReadOnlyDictionary<int, CompoundRecipe> _recipes;
    private readonly IReadOnlyList<CompounderList> _lists;
    private readonly CompoundPlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;
    private PendingCompound? _pending;

    public CompoundService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        IReadOnlyDictionary<int, CompoundRecipe> recipes,
        IReadOnlyList<CompounderList> lists,
        CompoundPlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _exchange = new MaterialExchange(items, catalog);
        _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public void Enter()
    {
        IsOpen = true;
        _pending = null;
    }

    public async Task<int> OpenAsync(string compounderTable, CancellationToken ct)
    {
        var list = _lists.FirstOrDefault(candidate => string.Equals(candidate.Name, compounderTable, StringComparison.Ordinal));
        if (list is null)
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   compound: no compounder list '{compounderTable}'");
            return NpcScript.Refused;
        }
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Enter();
        var visible = list.Entries.Where(entry => !entry.Hidden).Select(entry => entry.RecipeId).ToList();
        await SendAsync(Opcode.S2C_COMPOUND_LIST, CompoundFrames.List(list.Grade, visible), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   compound opened: {list.Name}, {visible.Count} recipes");
        return NpcScript.Silent;
    }

    public async Task RecipeAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !CompoundFrames.TryReadRecipeRequest(payload.Span, out var recipeId)) return;
        if (_recipes.TryGetValue(recipeId, out var recipe))
        {
            await SendAsync(Opcode.S2C_COMPOUND_RECIPE, CompoundFrames.Recipe(recipe), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_COMPOUND_RECIPE_MISSING, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task PercentAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !CompoundFrames.TryReadProbabilityRequest(payload.Span, out var recipeId, out var mode, out var count, out var materials)) return;
        if (_player.Character() is not { } character) return;
        _recipes.TryGetValue(recipeId, out var recipe);
        if (materials.Count > CompoundRules.MaxMaterials
            || CompoundRules.CheckRecipe(recipe, materials, count) != CompoundRules.Matches
            || !_exchange.BagHolds(character.CharacterId, materials))
        {
            await RefuseAsync(CompoundFrames.RefusedCode, $"recipe {recipeId} x{count} does not match the {materials.Count} materials", ct).ConfigureAwait(false);
            return;
        }
        int percent;
        CompoundRoll? roll;
        lock (_rng)
        {
            percent = CompoundRules.Percent(recipe, _player.Da(), () => _rng.Next(100));
            roll = CompoundRules.Roll(mode, percent, count, () => _rng.Next(100));
        }
        if (roll is not { } rolled)
        {
            await RefuseAsync(CompoundFrames.RefusedCode, $"mode {mode} is not a compound mode", ct).ConfigureAwait(false);
            return;
        }
        _pending = new PendingCompound(recipeId, count, materials, rolled);
        await SendAsync(Opcode.S2C_COMPOUND_PERCENT, CompoundFrames.Percent(rolled.Percent), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   compound {recipeId} x{count} mode {mode}: {rolled.Percent}% -> {rolled.Successes} made, {rolled.Failures} failed");
    }

    public async Task CompoundAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !CompoundFrames.TryReadMaterials(payload.Span, out var materials)) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (_pending is not { } pending || !SameMaterials(pending.Materials, materials))
        {
            await RefuseAsync(CompoundFrames.RefusedCode, "the materials differ from the rate request", ct).ConfigureAwait(false);
            return;
        }
        if (!_recipes.TryGetValue(pending.RecipeId, out var recipe))
        {
            await RefuseAsync(CompoundFrames.RefusedCode, $"recipe {pending.RecipeId} is unknown", ct).ConfigureAwait(false);
            return;
        }
        var fee = CompoundRules.Fee(recipe.Fee, pending.Count);
        if (fee < 0 || stats.Money < fee)
        {
            await RefuseAsync(CompoundFrames.RefusedCode, $"fee {fee} with {stats.Money} carried", ct).ConfigureAwait(false);
            return;
        }
        if (!_exchange.BagHolds(character.CharacterId, materials) || CompoundRules.CheckRecipe(recipe, materials, pending.Count) != CompoundRules.Matches)
        {
            await RefuseAsync(CompoundFrames.RefusedCode, "the compound condition is no longer valid", ct).ConfigureAwait(false);
            return;
        }

        var roll = pending.Roll;
        var results = roll.GivesResults ? recipe.Results : Array.Empty<int>();
        var wasteItem = roll.GivesResults ? recipe.WasteItem : 0;
        var grants = new List<(int ItemId, int Count)>();
        foreach (var result in results) grants.Add((result, roll.Successes));
        if (wasteItem != 0) grants.Add((wasteItem, roll.Failures));
        if (!_exchange.AllKnown(grants.Select(grant => grant.ItemId)))
        {
            await RefuseAsync(CompoundFrames.RefusedCode, $"recipe {recipe.Id} names an unknown item", ct).ConfigureAwait(false);
            return;
        }
        if (_exchange.Apply(character.CharacterId, materials, grants) is not { } operations)
        {
            await RefuseAsync(CompoundFrames.NoBagSpaceCode, "not enough free bag slots", ct).ConfigureAwait(false);
            return;
        }
        if (fee != 0) stats.GrantMoney(-(int)fee, StatChangeSource.Compound);
        if (fee != 0) await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_COMPOUND_RESULT,
            CompoundFrames.Result(results, wasteItem, roll.Successes, roll.Failures, operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   compound {recipe.Id} x{pending.Count}: {roll.Successes} made, {roll.Failures} failed, fee {fee}, {operations.Count} bag changes");
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        IsOpen = false;
        await SendAsync(Opcode.S2C_COMPOUND_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private static bool SameMaterials(IReadOnlyList<CompoundMaterial> stored, IReadOnlyList<CompoundMaterial> sent)
    {
        if (stored.Count != sent.Count) return false;
        for (var i = 0; i < stored.Count; i++)
        {
            if (stored[i].ItemId == 0 || stored[i] != sent[i]) return false;
        }
        return true;
    }

    private async Task RefuseAsync(int code, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   compound refused: {reason}");
        await SendAsync(Opcode.S2C_COMPOUND_REFUSED, CompoundFrames.Refused(code), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
