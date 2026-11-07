using System.ComponentModel;
using System.Diagnostics;
using tPoseidon.Hosting.Logs;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting;

public sealed record ClientStartCommand(string Executable, IReadOnlyList<string> Arguments, string WorkingDirectory, string? LogFile);

public interface IClientProcess
{
    Task<int> Finished { get; }
}

public interface IClientProcessRunner
{
    ToolResult Run(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment);

    IClientProcess Start(ClientStartCommand command);
}

public sealed class SystemClientProcessRunner : IClientProcessRunner
{
    public ToolResult Run(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string>? environment)
    {
        try
        {
            return ToolRunner.Capture(executable, arguments, environment);
        }
        catch (Win32Exception ex)
        {
            throw new HostingException($"{executable} could not be started: {ex.Message}", ex);
        }
    }

    public IClientProcess Start(ClientStartCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var info = new ProcessStartInfo(command.Executable)
        {
            WorkingDirectory = command.WorkingDirectory,
            UseShellExecute = false,
        };
        foreach (var argument in command.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var logged = command.LogFile is not null;
        info.RedirectStandardOutput = logged;
        info.RedirectStandardError = logged;
        try
        {
            var process = Process.Start(info) ?? throw new HostingException("The game client did not start.");
            return new SystemClientProcess(process, command.LogFile);
        }
        catch (Win32Exception ex)
        {
            throw new HostingException($"The game client could not be started: {ex.Message}", ex);
        }
    }

    private sealed class SystemClientProcess : IClientProcess
    {
        public SystemClientProcess(Process process, string? logFile)
        {
            Finished = Task.Run(() => Watch(process, logFile));
        }

        public Task<int> Finished { get; }

        private static int Watch(Process process, string? logFile)
        {
            using (process)
            {
                if (logFile is null)
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                using var writer = new StreamWriter(new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
                var paths = new PersonalPaths();
                var gate = new object();
                void Pump(StreamReader reader)
                {
                    while (reader.ReadLine() is { } line)
                    {
                        lock (gate)
                        {
                            writer.WriteLine(paths.Hide(line));
                        }
                    }
                }

                var output = Task.Run(() => Pump(process.StandardOutput));
                var error = Task.Run(() => Pump(process.StandardError));
                process.WaitForExit();
                Task.WaitAll(output, error);
                return process.ExitCode;
            }
        }
    }
}

public sealed record ClientLaunch(string Target, Task<string?> Closed);
