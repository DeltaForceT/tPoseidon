using tPoseidon.Hosting;

namespace tPoseidon.Cli.Commands;

internal static class CommandSupport
{
    public static HostSettings LoadSettings(InstallPaths paths) => HostSettings.LoadRequired(paths);
}
