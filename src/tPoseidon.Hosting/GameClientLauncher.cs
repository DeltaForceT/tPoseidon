using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using tPoseidon.Hosting.Database;
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
            $"{launch.ServiceName},{launch.Account},{launch.ServerDescription},{launch.WorldName},{launch.GroupName},{launch.WorldId},{settings.Network.PublicAddress}|world|group|{settings.Ports.Lobby}");
    }

    public static string Launch(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireAccount(settings);
        DatabaseAdmin.IssueLaunchTicket(settings.Database, settings.Launch.Account, NetworkAddresses.OfThisComputer());
        return Start(settings);
    }

    public static string LaunchWithTicket(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireAccount(settings);
        return Start(settings);
    }

    private static void RequireAccount(HostSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Launch.Account))
        {
            throw new HostingException("No game account is set for Play yet. Run tposeidon setup (or the panel's setup) to create your account.");
        }
    }

    private static string Start(HostSettings settings)
    {
        var check = ClientFolder.Check(settings.ClientFolder);
        if (!check.Ok)
        {
            throw new HostingException($"The client folder {settings.ClientFolder} is not usable ({string.Join("; ", check.Problems)}). Run tposeidon setup again.");
        }

        ClientFolder.PrepareEnglishMode(settings.ClientFolder);

        var executable = Path.Combine(settings.ClientFolder, check.Executable);
        ClientAddressPatch.Apply(executable, settings.Network.PublicAddress, settings.Ports.Lobby);
        ProcessStartInfo info;
        if (OperatingSystem.IsWindows())
        {
            info = new ProcessStartInfo(executable);
        }
        else
        {
            var wine = ToolRunner.FindOnPath("wine")
                ?? throw new HostingException("The game client is a Windows program. Install Wine (or CrossOver) to run it here, or play from a Windows PC.");
            info = new ProcessStartInfo(wine);
            info.ArgumentList.Add(executable);
        }

        info.ArgumentList.Add(LaunchArgument(settings));
        info.WorkingDirectory = settings.ClientFolder;
        info.UseShellExecute = false;
        try
        {
            using var process = Process.Start(info) ?? throw new HostingException("The game client did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new HostingException($"The game client could not be started: {ex.Message}", ex);
        }

        return $"{settings.Network.PublicAddress}:{settings.Ports.Lobby}";
    }
}
