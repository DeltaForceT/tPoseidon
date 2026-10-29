using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Panel;

public static class GmSpawnPage
{
    public const int LargeSpawn = 20;
    public const int MostNpcLines = byte.MaxValue;

    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var model = kit.Model;
        var place = new SpawnPlace(kit);

        var monster = GmPickers.Monster(model);
        var count = GmKit.Box("Count", 70, "1");
        var radius = GmKit.Box("Radius", 70, "0");
        var monstersSection = GmKit.Section(
            "Monsters",
            string.Create(CultureInfo.InvariantCulture, $"Monster types {GameMasterFrames.FirstSummonableMonster} to {GameMasterFrames.LastSummonableMonster}. They appear at the place chosen above, spread over the radius; your GM does not move. The server remembers them for \"Remove only the monsters I spawned\"."),
            monster,
            GmKit.Line(
                GmKit.Field("How many (1 to 255)", count),
                GmKit.Presets(count, ("1", "1"), ("5", "5"), ("10", "10"), ("20", "20")),
                GmKit.Field("Spread radius (0 = one spot)", radius),
                GmKit.Presets(radius, ("0", "0"), ("100", "100"), ("300", "300"), ("600", "600")),
                kit.Tool("Spawn monsters", "spawn-monsters", checks =>
                {
                    var type = checks.Number(monster.Id, "The monster type", GameMasterFrames.FirstSummonableMonster, GameMasterFrames.LastSummonableMonster);
                    return [type, checks.Number(count, "The count", 1, byte.MaxValue), checks.Number(radius, "The radius", 0, ushort.MaxValue), .. place.Arguments(checks)];
                }, arguments => int.Parse(arguments[1], CultureInfo.InvariantCulture) > LargeSpawn
                    ? new GmConfirm($"Spawn {arguments[1]} monsters?", "That is a lot of monsters on one spot. Players nearby will be attacked by all of them.", "Spawn")
                    : null, primary: true)),
            GmKit.Line(
                kit.Tool("Spawn with the original order (no spread)", "summon-monster", checks =>
                {
                    var type = checks.Number(monster.Id, "The monster type", GameMasterFrames.FirstSummonableMonster, GameMasterFrames.LastSummonableMonster);
                    return [type, checks.Number(count, "The count", 1, byte.MaxValue), .. place.OriginalOrderArguments(checks)];
                })));

        var npc = GmPickers.Npc(model);
        var lines = GmKit.Lines("Lines the NPC keeps, one per line (optional)");
        var npcSection = GmKit.Section(
            "NPCs",
            "Places an NPC where your GM stands. It stays until the game server restarts.",
            npc,
            lines,
            GmKit.Line(kit.Tool("Place NPC", "summon-npc", checks =>
            {
                var type = checks.Number(npc.Id, "The NPC type", 0, uint.MaxValue);
                var kept = (lines.Text ?? "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToList();
                checks.Require(kept.Count <= MostNpcLines, lines, string.Create(CultureInfo.InvariantCulture, $"An NPC keeps at most {MostNpcLines} lines."));
                return [type, .. kept];
            }, primary: true)));

        var give = GmPickers.Item(model);
        var handOutSection = GmKit.Section(
            "NPC hands out an item",
            "Lets an NPC on your GM's map hand one item out to each player who talks to it. To put an item in one player's bag use the Players tab.",
            give,
            IssueLine(kit, give));

        var objectId = GmKit.Box("Object id", 110);
        var clearSection = GmKit.Section(
            "Clear",
            "Remove only the monsters your GM spawned (on any map), every monster on your GM's map (spawned or not), or one monster by its object id.",
            GmKit.Line(
                kit.Tool("Remove only the monsters I spawned", "remove-my-monsters", _ => [], primary: true),
                kit.Tool("Remove every monster on my GM's map", "remove-monsters", _ => [],
                    _ => new GmConfirm("Remove every monster on your GM's map?", "This also removes the map's own monsters until they come back.", "Remove")),
                GmKit.Field("Object id", objectId),
                kit.Tool("Remove this monster", "remove-object", checks => [checks.Number(objectId, "The object id", 1, ushort.MaxValue)])));
        return GmKit.Tab(
            "Place monsters and NPCs where your GM stands or on a map you choose, and clear them again.",
            ("Where", [place.Section]),
            ("Monsters", [monstersSection, clearSection]),
            ("NPCs", [npcSection, handOutSection]));
    }

    private static WrapPanel IssueLine(GmKit kit, GmPicker item)
    {
        var npcObject = GmKit.Box("NPC object id", 120);
        var times = GmKit.Box("Times", 80, "10");
        return GmKit.Line(
            GmKit.Field("NPC object id", npcObject),
            GmKit.Field("How many players", times),
            kit.Tool("Let the NPC hand it out", "issue-item", checks =>
            {
                var npc = checks.Number(npcObject, "The NPC object id", 1, ushort.MaxValue);
                var id = checks.Number(item.Id, "The item id", 0, uint.MaxValue);
                return [npc, id, checks.Number(times, "How many players", 1, ushort.MaxValue)];
            }));
    }

    private sealed class SpawnPlace
    {
        private const int OnMap = 2;
        private const int AtCoordinates = 3;

        private readonly GmKit _kit;
        private readonly GmPicker _arrivalMap;
        private readonly GmPicker _coordinateMap;
        private readonly TextBox _x = GmKit.Box("X", 80);
        private readonly TextBox _y = GmKit.Box("Y", 80);
        private int _chosen;

        public SpawnPlace(GmKit kit)
        {
            _kit = kit;
            var model = kit.Model;
            _arrivalMap = GmPickers.Map(model);
            _coordinateMap = GmPickers.Map(model);
            var player = GmKit.Box("Character name", 200);
            model.CharacterChosen += chosen => player.Text = chosen;
            var atGm = new TextBlock { Text = "Spawns appear where your GM stands right now. Your GM does not move.", Foreground = Tokens.TextSecondary, Margin = new Thickness(0, 0, 0, 6) };
            var nextToPlayer = new StackPanel { Spacing = 4 };
            nextToPlayer.Children.Add(new TextBlock { Text = "Moves your GM next to the player first, then spawns where your GM stands.", Foreground = Tokens.TextSecondary });
            nextToPlayer.Children.Add(GmKit.Line(
                GmKit.Field("Player", player),
                kit.Tool("Move my GM next to them", "teleport", checks => [checks.Text(player, "The character name")], primary: true)));
            var onMap = new StackPanel { Spacing = 4 };
            onMap.Children.Add(new TextBlock { Text = "Spawns appear at a random arrival point of the map. Your GM stays where he is.", Foreground = Tokens.TextSecondary });
            onMap.Children.Add(_arrivalMap);
            var atCoordinates = new StackPanel { Spacing = 4 };
            atCoordinates.Children.Add(new TextBlock { Text = "Spawns appear around these coordinates of the map (map 0 = your GM's map). Your GM stays where he is.", Foreground = Tokens.TextSecondary });
            atCoordinates.Children.Add(_coordinateMap);
            atCoordinates.Children.Add(GmKit.Line(GmKit.Field("X", _x), GmKit.Field("Y", _y)));
            var places = new Control[] { atGm, nextToPlayer, onMap, atCoordinates };
            var labels = new[] { "Where my GM stands", "Next to a player", "Random spot on a map", "At coordinates" };
            var chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 0, 6) };
            var host = new ContentControl { Content = places[0] };
            for (var i = 0; i < labels.Length; i++)
            {
                var index = i;
                var chip = new ToggleButton { Content = labels[i], Height = 26, MinHeight = 0, Padding = new Thickness(10, 0), IsChecked = i == 0, VerticalContentAlignment = VerticalAlignment.Center, Foreground = Tokens.TextPrimary, FontSize = Tokens.FontSizeCaption };
                chip.Classes.Add("chip");
                chip.Click += (_, _) =>
                {
                    foreach (var other in chips.Children.OfType<ToggleButton>())
                    {
                        other.IsChecked = other == chip;
                    }

                    _chosen = index;
                    host.Content = places[index];
                };
                chips.Children.Add(chip);
            }

            Section = GmKit.Section(
                "Where",
                "Choose where the monsters appear. The server can spawn on another map or at coordinates without moving your GM there.",
                chips,
                host,
                GmKit.Line(kit.Tool("Where is my GM now?", "char-info", checks => [Gm(checks)])));
        }

        public Border Section { get; }

        public IReadOnlyList<string> Arguments(GmChecks checks) => _chosen switch
        {
            OnMap => [checks.Number(_arrivalMap.Id, "The map id", 1, 999)],
            AtCoordinates => [checks.Number(_coordinateMap.Id, "The map id", 0, 999), checks.Number(_x, "X", 0, ushort.MaxValue), checks.Number(_y, "Y", 0, ushort.MaxValue)],
            _ => [],
        };

        public IReadOnlyList<string> OriginalOrderArguments(GmChecks checks)
        {
            checks.Require(_chosen != AtCoordinates, null, "The original order has no coordinates: choose \"Where my GM stands\" or \"Random spot on a map\".");
            return _chosen == OnMap ? [checks.Number(_arrivalMap.Id, "The map id", 1, 999)] : [];
        }

        private string Gm(GmChecks checks)
        {
            checks.Require(_kit.Model.GameMaster is not null, null, "Connect first: your GM character is not known yet.");
            return _kit.Model.GameMaster ?? "";
        }
    }
}
