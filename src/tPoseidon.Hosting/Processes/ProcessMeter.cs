using System.ComponentModel;
using System.Diagnostics;

namespace tPoseidon.Hosting.Processes;

public readonly record struct ProcessUsage(double CpuPercent, double MemoryMegabytes);

public sealed class ProcessMeter
{
    private const double BytesPerMegabyte = 1024 * 1024;

    private readonly Dictionary<int, (TimeSpan Cpu, long Stamp)> _previous = new();

    public ProcessUsage? Sample(int processId)
    {
        TimeSpan cpu;
        long workingSet;
        try
        {
            using var process = Process.GetProcessById(processId);
            cpu = process.TotalProcessorTime;
            workingSet = process.WorkingSet64;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or NotSupportedException)
        {
            _previous.Remove(processId);
            return null;
        }

        var stamp = Stopwatch.GetTimestamp();
        var percent = 0.0;
        if (_previous.TryGetValue(processId, out var last))
        {
            var elapsed = Stopwatch.GetElapsedTime(last.Stamp, stamp).TotalSeconds;
            if (elapsed > 0)
            {
                percent = Math.Clamp((cpu - last.Cpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100, 0, 100);
            }
        }

        _previous[processId] = (cpu, stamp);
        return new ProcessUsage(percent, workingSet / BytesPerMegabyte);
    }
}
