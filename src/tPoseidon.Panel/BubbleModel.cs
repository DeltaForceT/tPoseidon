using Avalonia;
using Avalonia.Controls;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Panel;

public enum BubbleState
{
    Stopped,
    Partial,
    Running,
    Failed,
}

public static class BubbleStatus
{
    public static BubbleState From(IEnumerable<ServiceModel> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var servers = services.Where(service => !service.IsDatabase).ToList();
        if (servers.Any(service => service.Status == ServerStatus.Crashed))
        {
            return BubbleState.Failed;
        }

        var running = servers.Count(service => service.Status == ServerStatus.Running);
        if (servers.Count > 0 && running == servers.Count)
        {
            return BubbleState.Running;
        }

        var starting = servers.Count(service => service.Status == ServerStatus.Starting);
        return running + starting > 0 ? BubbleState.Partial : BubbleState.Stopped;
    }

    public static string Describe(BubbleState state, IEnumerable<ServiceModel> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var servers = services.Where(service => !service.IsDatabase).ToList();
        return state switch
        {
            BubbleState.Running => $"All {servers.Count} servers running",
            BubbleState.Partial => $"{servers.Count(service => service.Status == ServerStatus.Running)} of {servers.Count} servers running",
            BubbleState.Failed => $"{servers.First(service => service.Status == ServerStatus.Crashed).Name} failed",
            _ => "Servers stopped",
        };
    }
}

public static class BubblePlacement
{
    public const double BodySize = 104;
    public const double Pad = 16;
    public const double CellSize = BodySize + Pad * 2;

    public static bool ShouldShow(bool enabled, bool panelMinimized, bool setupOpen) => enabled && panelMinimized && !setupOpen;

    public static PixelPoint Clamp(PixelPoint topLeft, int size, IReadOnlyList<PixelRect> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);
        if (areas.Count == 0)
        {
            return topLeft;
        }

        var center = new PixelPoint(topLeft.X + size / 2, topLeft.Y + size / 2);
        var area = areas.Where(candidate => candidate.Contains(center)).Cast<PixelRect?>().FirstOrDefault() ?? Nearest(center, areas);
        return new PixelPoint(
            Math.Clamp(topLeft.X, area.X, Math.Max(area.X, area.Right - size)),
            Math.Clamp(topLeft.Y, area.Y, Math.Max(area.Y, area.Bottom - size)));
    }

    public static PixelPoint Default(PixelRect area, int size) =>
        new(area.X + (area.Width - size) / 2, area.Y + (area.Height - size) / 2);

    public static (bool OnRight, bool OnBottom) Quadrant(PixelPoint topLeft, int size, PixelRect area) =>
        (topLeft.X + size / 2 > area.X + area.Width / 2, topLeft.Y + size / 2 > area.Y + area.Height / 2);

    public static PixelPoint WindowPosition(PixelPoint bodyTopLeft, double scale, Size window, bool onRight, bool onBottom)
    {
        var cellLeft = bodyTopLeft.X - (int)Math.Round(Pad * scale);
        var cellTop = bodyTopLeft.Y - (int)Math.Round(Pad * scale);
        var x = onRight ? cellLeft - (int)Math.Round((window.Width - CellSize) * scale) : cellLeft;
        var y = onBottom ? cellTop - (int)Math.Round((window.Height - CellSize) * scale) : cellTop;
        return new PixelPoint(x, y);
    }

    private static PixelRect Nearest(PixelPoint point, IReadOnlyList<PixelRect> areas) =>
        areas.OrderBy(area =>
        {
            var dx = point.X - Math.Clamp(point.X, area.X, area.Right);
            var dy = point.Y - Math.Clamp(point.Y, area.Y, area.Bottom);
            return (long)dx * dx + (long)dy * dy;
        }).First();
}

public sealed record BubbleTool(string Id, string Glyph, string Tip, Func<bool> Enabled, Func<Control, Task> Run, bool ClosesBubble);

public sealed record BubbleGroup(string Name, IReadOnlyList<BubbleTool> Tools);

public static class BubbleCatalog
{
    public const string StartAll = "start-all";
    public const string StopAll = "stop-all";
    public const string RestartAll = "restart-all";
    public const string Logs = "logs";
    public const string GameMaster = "game-master";
    public const string Language = "language";
    public const string Play = "play";
    public const string ShareFile = "share-file";
    public const string Launcher = "launcher";
    public const string LauncherNews = "launcher-news";
    public const string PublishUpdate = "publish-update";
    public const string OpenPanel = "open-panel";
    public const string Security = "security";
    public const string Backup = "backup";
    public const string DataFolder = "data-folder";
    public const string Exit = "exit";

    public static IReadOnlyList<BubbleGroup> Build(PanelTools tools, Action openPanel, Action exit)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(openPanel);
        ArgumentNullException.ThrowIfNull(exit);
        Func<bool> setUp = () => tools.Host.IsSetUp;
        Func<bool> always = () => true;
        return
        [
            new BubbleGroup("Servers",
            [
                new BubbleTool(StartAll, Glyph.Play, "Start all servers, one by one", setUp, _ => tools.StartAllAsync(), false),
                new BubbleTool(StopAll, Glyph.Stop, "Stop all servers. Postgres keeps running", setUp, _ => tools.Host.StopAllAsync(), false),
                new BubbleTool(RestartAll, Glyph.Restart, "Stop and start all servers", setUp, _ => tools.Host.RestartAllAsync(), false),
                new BubbleTool(Logs, Glyph.Log, "Show the logs", always, Sync(_ => PanelTools.OpenLogs()), true),
            ]),
            new BubbleGroup("Game",
            [
                new BubbleTool(GameMaster, Glyph.Admin, "Game master tools: players, spawns, events, guilds and GM accounts", always, Sync(_ => tools.OpenGameMaster()), true),
                new BubbleTool(Language, Glyph.Language, "Client language", setUp, Sync(tools.OpenLanguageMenu), false),
                new BubbleTool(Play, Glyph.Client, "Launch the game client", setUp, Sync(_ => tools.Host.LaunchClient()), false),
            ]),
            new BubbleGroup("Players",
            [
                new BubbleTool(ShareFile, Glyph.Share, "Share a server file for friends", setUp, Sync(_ => tools.ShareServerFile()), true),
                new BubbleTool(Launcher, Glyph.Launcher, "Make a launcher for your players", setUp, Sync(_ => tools.MakeLauncher()), true),
                new BubbleTool(LauncherNews, Glyph.News, "Edit launcher news and updates", setUp, Sync(_ => tools.EditLauncherChannel()), true),
                new BubbleTool(PublishUpdate, Glyph.Publish, "Publish launcher update", setUp, _ => tools.Host.PublishLauncherUpdateAsync(), false),
            ]),
            new BubbleGroup("Panel",
            [
                new BubbleTool(OpenPanel, Glyph.Panel, "Open the panel", always, Sync(_ => openPanel()), true),
                new BubbleTool(Security, Glyph.Shield, "Blocked addresses, incidents and bans from the connection guard", always, Sync(_ => tools.OpenSecurity()), true),
                new BubbleTool(Backup, Glyph.Backup, "Back up the database", setUp, _ => tools.Host.BackupAsync(), false),
                new BubbleTool(DataFolder, Glyph.Folder, "Open the data folder", always, _ => tools.OpenDataFolderAsync(), false),
                new BubbleTool(Exit, Glyph.Exit, "Exit tPoseidon. The servers keep running", always, Sync(_ => exit()), true),
            ]),
        ];
    }

    private static Func<Control, Task> Sync(Action<Control> action) =>
        source =>
        {
            action(source);
            return Task.CompletedTask;
        };
}
