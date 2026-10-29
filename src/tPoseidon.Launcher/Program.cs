using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrash(e.ExceptionObject);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();

    private static void WriteCrash(object error)
    {
        var folder = Path.GetDirectoryName(LauncherSettings.DefaultPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "launcher-crash.log"), $"{DateTime.Now:O}{Environment.NewLine}{error}");
    }
}

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Skins.Register(Styles);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Open(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window Open(string[] args)
    {
        Branding? branding;
        try
        {
            branding = Branding.Find(AppContext.BaseDirectory);
        }
        catch (HostingException ex)
        {
            return new BrokenBrandingWindow(ex);
        }

        var path = LauncherSettings.PathFor(branding);
        var settings = LauncherSettings.Load(path, out var problem);
        IClientSetupStore? setup = OperatingSystem.IsWindows() ? new RegistryClientSetupStore() : null;
        var model = new LauncherModel(settings, path, new HttpTicketService(), new LocalClientHost(), new ClientUpdater(HttpUpdateSource.Create(), TimeSpan.FromSeconds(2)), branding, setup);
        var startupFile = args.FirstOrDefault(arg => arg.EndsWith(ServerFile.Extension, StringComparison.OrdinalIgnoreCase));
        if (startupFile is not null)
        {
            model.ImportServerFile(startupFile);
        }

        if (problem is not null)
        {
            model.ReportProblem(problem);
        }

        return new LauncherWindow(model);
    }
}
