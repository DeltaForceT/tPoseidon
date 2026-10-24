using System.Diagnostics;

namespace tPoseidon.Hosting.Servers;

public static class DetachedLauncher
{
    private static readonly object EnvironmentGate = new();
    private const string UnixShell = "/bin/sh";
    private static readonly string[] SetsidLocations = { "/usr/bin/setsid", "/bin/setsid" };

    public static Process Start(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory)
    {
        try
        {
            return OperatingSystem.IsWindows()
                ? StartWindows(executable, arguments, environment, workingDirectory)
                : StartUnix(executable, arguments, environment, workingDirectory);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new HostingException($"Could not start {executable}: {ex.Message}", ex);
        }
    }

    private static Process StartWindows(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory)
    {
        lock (EnvironmentGate)
        {
            var previous = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);
            try
            {
                foreach (var pair in environment)
                {
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }

                var info = new ProcessStartInfo(executable)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workingDirectory,
                    Arguments = string.Join(' ', arguments.Select(Quote)),
                };
                return Process.Start(info) ?? throw new HostingException($"Could not start {executable}.");
            }
            finally
            {
                foreach (var pair in previous)
                {
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }
            }
        }
    }

    private static Process StartUnix(
        string executable,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        string workingDirectory)
    {
        var detach = SetsidLocations.FirstOrDefault(File.Exists) is { } setsid ? $"exec '{setsid}'" : "exec nohup";
        var info = new ProcessStartInfo(UnixShell)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(detach + " \"$0\" \"$@\" </dev/null >/dev/null 2>&1");
        info.ArgumentList.Add(executable);
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var pair in environment)
        {
            info.Environment[pair.Key] = pair.Value;
        }

        return Process.Start(info) ?? throw new HostingException($"Could not start {executable}.");
    }

    private static string Quote(string argument) =>
        argument.Length > 0 && argument.All(c => !char.IsWhiteSpace(c) && c != '"')
            ? argument
            : "\"" + argument.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
