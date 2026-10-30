using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Hosting.GameMaster;

public sealed record CatalogMonster(uint Type, string Name, int Level, bool Boss, string Picture = "");

public sealed record CatalogSkill(int Id, string Name, int MaxLevel, string IconFile, int IconBundle);

public sealed record CatalogNpc(uint Type, string Name, string Picture = "");

public sealed record CatalogMap(ushort Id, string Name, IReadOnlySet<uint> Monsters);

public sealed record ChatCommandHelp(string Name, string Keyword, string Arguments, string Description)
{
    public string Typed => $"{GameMasterChat.CommandMark}{Keyword}{(Arguments.Length == 0 ? string.Empty : " " + Arguments)}";
}

public sealed class GameCatalog
{
    private GameCatalog(string? source, string? problem, ItemNames items, IReadOnlyList<CatalogMonster> monsters, IReadOnlyList<CatalogNpc> npcs, IReadOnlyList<CatalogMap> maps, IReadOnlyList<CatalogSkill> skills, IReadOnlyList<MyCampItemEntry> campItems)
    {
        Source = source;
        Problem = problem;
        Items = items;
        Monsters = monsters;
        Npcs = npcs;
        Maps = maps;
        Skills = skills;
        CampItems = campItems;
        ChatCommands = GameMasterChat.Commands.Select(command => new ChatCommandHelp(command.Name, command.Keyword, command.Arguments, command.Description)).ToList();
    }

    public string? Source { get; }

    public string? Problem { get; }

    public ItemNames Items { get; }

    public IReadOnlyList<CatalogMonster> Monsters { get; }

    public IReadOnlyList<CatalogNpc> Npcs { get; }

    public IReadOnlyList<CatalogMap> Maps { get; }

    public IReadOnlyList<CatalogSkill> Skills { get; }

    public IReadOnlyList<MyCampItemEntry> CampItems { get; }

    public IReadOnlyList<ChatCommandHelp> ChatCommands { get; }

    public static GameCatalog Empty(string problem) =>
        new(null, problem, new ItemNames(Array.Empty<(string, ItemParamEntry)>()), Array.Empty<CatalogMonster>(), Array.Empty<CatalogNpc>(), Array.Empty<CatalogMap>(), Array.Empty<CatalogSkill>(), Array.Empty<MyCampItemEntry>());

    public static GameCatalog For(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var libconfig = ClientFolder.LibConfigFor(settings);
        return File.Exists(libconfig)
            ? Load(libconfig)
            : Empty($"The game data file {libconfig} was not found, so the pickers show ids only. Check the client folder in the setup.");
    }

    public static GameCatalog Load(string libconfigPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigPath);
        var reader = new LibConfigXmlReader(libconfigPath);
        var characters = reader.LoadCharacterInfo();
        var pictures = characters
            .GroupBy(entry => entry.Id)
            .ToDictionary(group => group.Key, group => group.First().FileName.Trim());
        var monsters = reader.LoadMonsterParamEx2()
            .Where(entry => entry.Id >= 0 && GameMasterFrames.IsSummonableMonster((uint)entry.Id) && !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .Select(entry => new CatalogMonster((uint)entry.Id, entry.Name.Trim(), entry.Level, entry.IsBoss, pictures.GetValueOrDefault(entry.Id, string.Empty)))
            .OrderBy(monster => monster.Type)
            .ToList();
        var npcs = characters
            .Where(entry => NpcManager.IsValidChrType(entry.Id))
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .Select(entry => new CatalogNpc((uint)entry.Id, entry.DisplayName, entry.FileName.Trim()))
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
        var skills = reader.LoadSkillParam2()
            .Where(entry => entry.Id > 0 && !string.IsNullOrWhiteSpace(entry.Name))
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .Select(entry => new CatalogSkill(entry.Id, entry.Name.Trim(), Math.Min(entry.MaxSkillLevel, SkillDefinition.HighestLevel), entry.IconFile, entry.IconBundle))
            .OrderBy(skill => skill.Id)
            .ToList();
        var campItems = reader.LoadMyCampItems()
            .Where(entry => MyCampGifts.IsItemIndex(entry.Id) && entry.Name.Length > 0)
            .GroupBy(entry => entry.Id)
            .Select(group => group.First())
            .OrderBy(entry => entry.Id)
            .ToList();
        return new GameCatalog(libconfigPath, null, ItemNames.Load(libconfigPath), monsters, npcs, maps, skills, campItems);
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

    public IEnumerable<ItemName> FindItems(string text, string? table, byte? category = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Items.All
            .Where(item => (table is null || item.Table == table)
                && (category is null || PlayerShopSearchRules.CategoryOf(item.Definition) == category)
                && Matches(text, item.Name, (uint)item.Id))
            .OrderBy(item => item.Id);
    }

    public IEnumerable<CatalogSkill> FindSkills(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Skills.Where(skill => Matches(text, skill.Name, (uint)skill.Id));
    }

    public IEnumerable<MyCampItemEntry> FindCampItems(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CampItems.Where(item => Matches(text, item.Name, (uint)item.Id));
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
}
