using System.Diagnostics;

namespace tPoseidon.Hosting.Processes;

public sealed record ToolResult(int ExitCode, string Output);

public static class ToolRunner
{
    public static ToolResult Capture(string executable, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment = null)
    {
        var info = Build(executable, arguments, environment);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.CreateNoWindow = true;
        using var process = StartOrThrow(info);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new ToolResult(process.ExitCode, (stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult()).Trim());
    }

    public static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var candidates = OperatingSystem.IsWindows() ? new[] { name + ".exe", name } : new[] { name };
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in candidates)
            {
                var full = Path.Combine(directory, candidate);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }

    private static ProcessStartInfo Build(string executable, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                info.Environment[pair.Key] = pair.Value;
            }
        }

        return info;
    }

    private static Process StartOrThrow(ProcessStartInfo info)
    {
        try
        {
            return Process.Start(info) ?? throw new HostingException($"Could not start {info.FileName}.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new HostingException($"Could not start {info.FileName}: {ex.Message}", ex);
        }
    }
}
