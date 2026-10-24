using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Hosting.GameMaster;

public sealed record CatalogMonster(uint Type, string Name, int Level, bool Boss);

public sealed record CatalogNpc(uint Type, string Name);

public sealed record CatalogMap(ushort Id, string Name, IReadOnlySet<uint> Monsters);

public sealed record ChatCommandHelp(string Name, string? Keyword, string Arguments, string Description)
{
    public string Typed => Keyword is null ? string.Empty : $"{GameMasterChat.CommandMark}{Keyword}{(Arguments.Length == 0 ? string.Empty : " " + Arguments)}";
}

public sealed class GameCatalog
{
    private GameCatalog(string? source, string? problem, ItemNames items, IReadOnlyList<CatalogMonster> monsters, IReadOnlyList<CatalogNpc> npcs, IReadOnlyList<CatalogMap> maps, GameMasterChat chat)
    {
        Source = source;
        Problem = problem;
        Items = items;
        Monsters = monsters;
        Npcs = npcs;
        Maps = maps;
        ChatCommands = GameMasterChat.Commands.Select(command => new ChatCommandHelp(command.Name, chat.KeywordOf(command.Name), command.Arguments, command.Description)).ToList();
    }

    public string? Source { get; }

    public string? Problem { get; }

    public ItemNames Items { get; }

    public IReadOnlyList<CatalogMonster> Monsters { get; }

    public IReadOnlyList<CatalogNpc> Npcs { get; }

    public IReadOnlyList<CatalogMap> Maps { get; }

    public IReadOnlyList<ChatCommandHelp> ChatCommands { get; }

    public static GameCatalog Empty(string problem) =>
        new(null, problem, new ItemNames(Array.Empty<(string, ItemParamEntry)>()), Array.Empty<CatalogMonster>(), Array.Empty<CatalogNpc>(), Array.Empty<CatalogMap>(), NoChat());

    public static GameCatalog For(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var libconfig = ClientFolder.LibConfigFor(settings.ClientFolder, settings.Launch.ServiceName);
        return File.Exists(libconfig)
            ? Load(libconfig)
            : Empty($"The game data file {libconfig} was not found, so the pickers show ids only. Check the client folder in the setup.");
    }

    public static GameCatalog Load(string libconfigPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigPath);
        var reader = new LibConfigXmlReader(libconfigPath);
        var monsters = reader.LoadMonsterParamEx2()
            .Where(entry => entry.Id >= 0 && GameMasterFrames.IsSummonableMonster((uint)entry.Id) && !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .Select(entry => new CatalogMonster((uint)entry.Id, entry.Name.Trim(), entry.Level, entry.IsBoss))
            .OrderBy(monster => monster.Type)
            .ToList();
        var npcs = reader.LoadCharacterInfo()
            .Where(entry => NpcManager.IsValidChrType(entry.Id))
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .Select(entry => new CatalogNpc((uint)entry.Id, entry.DisplayName))
            .OrderBy(npc => npc.Type)
            .ToList();
        var pools = reader.LoadAllMonsterPoolTables();
        var maps = reader.LoadMapInfo()
            .GroupBy(map => map.Id)
            .Select(group => group.First())
            .Where(map => !string.IsNullOrWhiteSpace(map.Name))
            .Select(map => new CatalogMap(map.Id, map.Name.Trim(), MonstersOf(map, pools)))
            .OrderBy(map => map.Id)
            .ToList();
        var chat = new GameMasterChat(reader.LoadNameStringTable(GameMasterChat.KeywordTable), new Dictionary<string, string>());
        return new GameCatalog(libconfigPath, null, ItemNames.Load(libconfigPath), monsters, npcs, maps, chat);
    }

    public string? MapName(int id) => Maps.FirstOrDefault(map => map.Id == id)?.Name;

    public IEnumerable<CatalogMonster> FindMonsters(string text, int minimumLevel, int maximumLevel, ushort? mapId)
    {
        ArgumentNullException.ThrowIfNull(text);
        var onMap = mapId is { } id ? Maps.FirstOrDefault(map => map.Id == id)?.Monsters : null;
        return Monsters.Where(monster =>
            monster.Level >= minimumLevel && monster.Level <= maximumLevel
            && (onMap is null || onMap.Contains(monster.Type))
            && Matches(text, monster.Name, monster.Type));
    }

    public IEnumerable<CatalogNpc> FindNpcs(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Npcs.Where(npc => Matches(text, npc.Name, npc.Type));
    }

    public IEnumerable<ItemName> FindItems(string text, string? table)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Items.All
            .Where(item => (table is null || item.Table == table) && Matches(text, item.Name, (uint)item.Id))
            .OrderBy(item => item.Id);
    }

    public IEnumerable<CatalogMap> FindMaps(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Maps.Where(map => Matches(text, map.Name, map.Id));
    }

    private static bool Matches(string text, string name, uint id)
    {
        var wanted = text.Trim();
        return wanted.Length == 0
            || name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || id.ToString(System.Globalization.CultureInfo.InvariantCulture).StartsWith(wanted, StringComparison.Ordinal);
    }

    private static HashSet<uint> MonstersOf(MapInfoEntry map, IReadOnlyDictionary<string, IReadOnlyList<MonsterPoolRow>> pools)
    {
        var monsters = new HashSet<uint>();
        foreach (var table in new[] { map.MonsterTable, map.BossMonsterTable })
        {
            if (table.Length == 0 || !pools.TryGetValue(table, out var rows)) continue;
            foreach (var row in rows)
            {
                if (row.MonsterId >= 0) monsters.Add((uint)row.MonsterId);
            }
        }

        return monsters;
    }

    private static GameMasterChat NoChat() => new(new Dictionary<string, string>(), new Dictionary<string, string>());
}
