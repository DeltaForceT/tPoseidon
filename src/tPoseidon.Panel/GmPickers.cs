using System.Globalization;
using Avalonia.Controls;
using tPoseidon.Core.LibConfig;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public static class GmPickers
{
    public const string AnyCategory = "Any category";
    public const string AnyTable = "Any table";
    public const string AnyMap = "Any map";

    public static GmPicker Map(GmModel model, string idLabel = "Map id") =>
        new(model, "maps", idLabel, (catalog, text) => catalog.FindMaps(text).Select(map => new GmPick(Text(map.Id), $"{map.Name} ({map.Id})")), false);

    public static GmPicker Npc(GmModel model) =>
        new(model, "NPCs", "NPC type", (catalog, text) => catalog.FindNpcs(text).Select(npc => new GmPick(Text(npc.Type), $"{npc.Name} · type {npc.Type}", Sprite(npc.Picture))), true);

    public static GmPicker Item(GmModel model)
    {
        var category = GmKit.Choice(new[] { AnyCategory }.Concat(ItemCategories.All.Select(entry => entry.Name)), 150);
        var table = GmKit.Choice(new[] { AnyTable }.Concat(ItemNames.Tables), 130);
        GmPicker? picker = null;
        picker = new GmPicker(
            model,
            "items",
            "Item id",
            (catalog, text) => catalog
                .FindItems(
                    text,
                    table.SelectedIndex > 0 ? ItemNames.Tables[table.SelectedIndex - 1] : null,
                    category.SelectedIndex > 0 ? ItemCategories.All[category.SelectedIndex - 1].Id : null)
                .Select(item => new GmPick(Text(item.Id), $"{item.Name} · {item.Id} · {item.Table}", Icon(item.Definition))),
            true,
            GmKit.Field("Category", category),
            GmKit.Field("Game data table", table));
        category.SelectionChanged += (_, _) => picker.Restart();
        table.SelectionChanged += (_, _) => picker.Restart();
        return picker;
    }

    public static GmPicker Monster(GmModel model)
    {
        var minimum = GmKit.Box("from", 56, "1");
        var maximum = GmKit.Box("to", 56, "999");
        var map = new ComboBox { Height = 28, MinHeight = 0, Width = 190, FontSize = Tokens.FontSizeBody, ItemsSource = new[] { AnyMap }, SelectedIndex = 0 };
        IReadOnlyList<CatalogMap> maps = [];
        GmPicker? picker = null;
        picker = new GmPicker(
            model,
            "monsters",
            "Monster type",
            (catalog, text) =>
            {
                if (maps.Count == 0 && catalog.Maps.Count > 0)
                {
                    maps = catalog.Maps.Where(entry => entry.Monsters.Count > 0).OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    map.ItemsSource = new[] { AnyMap }.Concat(maps.Select(entry => $"{entry.Name} ({entry.Id})")).ToList();
                    map.SelectedIndex = 0;
                }

                var low = int.TryParse(minimum.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var from) ? from : int.MinValue;
                var high = int.TryParse(maximum.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var to) ? to : int.MaxValue;
                ushort? onMap = map.SelectedIndex > 0 && map.SelectedIndex <= maps.Count ? maps[map.SelectedIndex - 1].Id : null;
                return catalog.FindMonsters(text, low, high, onMap)
                    .Select(monster => new GmPick(Text(monster.Type), $"{monster.Name} · Lv {monster.Level}{(monster.Boss ? " · boss" : "")} · type {monster.Type}", Sprite(monster.Picture)));
            },
            true,
            GmKit.Field("Level", Range(minimum, maximum)),
            GmKit.Field("Lives on", map));
        minimum.TextChanged += (_, _) => picker.Restart();
        maximum.TextChanged += (_, _) => picker.Restart();
        map.SelectionChanged += (_, _) => picker.Restart();
        return picker;
    }

    private static GmArt? Icon(ItemParamEntry definition) =>
        definition.InvFileName.Length == 0 || definition.InvBundleNum < 0 ? null : GmArt.Icon(definition.InvFileName, definition.InvBundleNum);

    private static GmArt? Sprite(string file) => file.Length == 0 ? null : GmArt.Sprite(file);

    private static StackPanel Range(TextBox minimum, TextBox maximum)
    {
        var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(minimum);
        row.Children.Add(new TextBlock { Text = "to", Foreground = Tokens.TextMuted, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        row.Children.Add(maximum);
        return row;
    }

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);
}
