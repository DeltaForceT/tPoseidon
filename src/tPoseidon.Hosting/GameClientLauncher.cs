using System.Globalization;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Logs;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting;

public static class GameClientLauncher
{
    public static string LaunchArgument(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var launch = settings.Launch;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ClientFolder.Service(settings)},{launch.Account},{launch.ServerDescription},{launch.WorldName},{launch.GroupName},{launch.WorldId},{settings.Network.PublicAddress}|world|group|{settings.Ports.Lobby}");
    }

    public static readonly TimeSpan QuickExitWindow = TimeSpan.FromSeconds(10);

    public const string WineLogName = "client-wine";

    public static ClientLaunch Launch(HostSettings settings, InstallPaths? paths = null, IClientProcessRunner? runner = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireAccount(settings);
        DatabaseAdmin.IssueLaunchTicket(settings.Database, settings.Launch.Account, NetworkAddresses.OfThisComputer());
        return Start(settings, paths, runner ?? new SystemClientProcessRunner());
    }

    public static ClientLaunch LaunchWithTicket(HostSettings settings, InstallPaths? paths = null, IClientProcessRunner? runner = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireAccount(settings);
        return Start(settings, paths, runner ?? new SystemClientProcessRunner());
    }

    public static ClientStartCommand Command(string? wine, string executable, string launchArgument, string workingDirectory, string? logFile) =>
        wine is null
            ? new ClientStartCommand(executable, [launchArgument], workingDirectory, null)
            : new ClientStartCommand(wine, [executable, launchArgument], workingDirectory, logFile);

    public static async Task<string?> WatchAsync(IClientProcess process, string? logFile, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(process);
        var finished = await Task.WhenAny(process.Finished, Task.Delay(window)).ConfigureAwait(false);
        if (finished != process.Finished)
        {
            return null;
        }

        var where = logFile is null ? "" : $" See {new PersonalPaths().Hide(logFile)}.";
        return $"The game client closed right after starting.{where}";
    }

    private static void RequireAccount(HostSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Launch.Account))
        {
            throw new HostingException("No game account is set for Play yet. Run tposeidon setup (or the panel's setup) to create your account.");
        }
    }

    public static bool ApplyDisplay(HostSettings settings, string executable, string service, IClientProcessRunner? runner = null, string? wine = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Launch.Display is not { } display)
        {
            return false;
        }

        var company = ClientSetupProfile.CompanyIn(File.ReadAllBytes(executable))
            ?? throw new HostingException("This client does not say where it keeps its settings, so the game window cannot be set.", "Use the KR 2014 client.", null);
        var profile = new ClientSetupProfile(company, ClientSetupProfile.NameFor(service));
        if (OperatingSystem.IsWindows())
        {
            RegistryClientDisplay.Write(profile, display);
        }
        else
        {
            WineClientDisplay.Write(runner ?? new SystemClientProcessRunner(), wine ?? FindWine(), profile, display);
        }

        return true;
    }

    private static string FindWine() =>
        ToolRunner.FindOnPath("wine")
            ?? throw new HostingException("The game client is a Windows program. Install Wine (or CrossOver) to run it here, or play from a Windows PC.");

    private static ClientLaunch Start(HostSettings settings, InstallPaths? paths, IClientProcessRunner runner)
    {
        var check = ClientFolder.Check(settings.ClientFolder);
        if (!check.Ok)
        {
            throw new HostingException($"The client folder {settings.ClientFolder} is not usable ({string.Join("; ", check.Problems)}). Run tposeidon setup again.");
        }

        var plan = ClientFolder.Prepare(settings);
        var executable = Path.Combine(settings.ClientFolder, check.Executable);
        ShadowSanctuaryClientData.Apply(plan.LibConfig, executable);
        ReferencedTablesClientData.Apply(plan.LibConfig, executable);
        ReferencedItemRows.Data.Apply(plan.LibConfig, executable);
        ClientAddressPatch.Apply(executable, settings.Network.PublicAddress, settings.Ports.Lobby);
        var wine = OperatingSystem.IsWindows() ? null : FindWine();
        ApplyDisplay(settings, executable, plan.Service, runner, wine);
        var logFile = wine is null || paths is null ? null : paths.LogFile(WineLogName);
        var command = Command(wine, executable, LaunchArgument(settings), settings.ClientFolder, logFile);
        var process = runner.Start(command);
        var closed = WatchAsync(process, logFile, QuickExitWindow);
        return new ClientLaunch($"{settings.Network.PublicAddress}:{settings.Ports.Lobby}", closed);
    }
}
