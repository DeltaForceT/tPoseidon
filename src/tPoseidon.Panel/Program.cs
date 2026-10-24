using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using tPoseidon.Hosting;

namespace tPoseidon.Panel;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = LaunchOptions.Parse(args);
        if (options.Problem is { } problem)
        {
            Console.Error.WriteLine(problem);
            Console.Error.WriteLine(LaunchOptions.Usage);
            return 2;
        }

        LaunchOptions.Current = options;
        var paths = InstallPaths.Resolve(options.Home);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrash(paths, e.ExceptionObject);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();

    private static void WriteCrash(InstallPaths paths, object error)
    {
        Directory.CreateDirectory(paths.Logs);
        File.WriteAllText(Path.Combine(paths.Logs, "panel-crash.log"), $"{DateTime.Now:O}{Environment.NewLine}{error}");
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
            var options = LaunchOptions.Current;
            var preferences = PanelPreferences.Load(out var problem);
            Motion.Shared.SetReduced(preferences.ReduceAnimations || options.Reduce);
            var host = new PanelHost(InstallPaths.Resolve(options.Home));
            var main = new MainWindow(host, preferences);
            main.Closed += (_, _) => desktop.Shutdown();
            void AfterIntro()
            {
                if (problem is not null)
                {
                    LogHub.Write(LogHub.PanelSource, LogLevel.Warn, problem);
                }

                if (options.Setup || !host.IsSetUp)
                {
                    main.OpenSetup();
                }
            }

            host.Reload();

            if (Motion.Shared.Reduced || options.NoSplash)
            {
                main.FirstShown += AfterIntro;
                desktop.MainWindow = main;
            }
            else
            {
                var splash = new SplashWindow(_ => desktop.MainWindow = main);
                splash.Closed += (_, _) => AfterIntro();
                splash.Bind(main);
                splash.PlaceOverScreenCenter();
                splash.Opened += (_, _) =>
                {
                    main.Show();
                    splash.Activate();
                    splash.Aim();
                };
                desktop.MainWindow = splash;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public sealed record LaunchOptions(string? Home, bool Setup, bool NoSplash, bool Reduce, string? Problem)
{
    public const string Usage = "Usage: tPoseidon.Panel [--home <folder>] [--setup] [--no-splash] [--reduce-animations]";

    public static LaunchOptions Current { get; set; } = new(null, false, false, false, null);

    public static LaunchOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? home = null;
        bool setup = false, noSplash = false, reduce = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--home" when i + 1 < args.Length:
                    home = args[++i];
                    break;
                case "--setup":
                    setup = true;
                    break;
                case "--no-splash":
                    noSplash = true;
                    break;
                case "--reduce-animations":
                    reduce = true;
                    break;
                default:
                    return new LaunchOptions(null, false, false, false, $"Unknown option '{args[i]}'.");
            }
        }

        return new LaunchOptions(home, setup, noSplash, reduce, null);
    }
}
