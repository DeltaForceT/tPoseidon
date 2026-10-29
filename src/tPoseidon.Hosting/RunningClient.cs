using System.ComponentModel;
using System.Diagnostics;

namespace tPoseidon.Hosting;

public static class RunningClient
{
    public static bool IsRunning(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var full = Path.GetFullPath(executablePath);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(full)))
        {
            using (process)
            {
                if (string.Equals(ImagePath(process), full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ImagePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
