using System.Globalization;
using System.Net;

namespace tPoseidon.Core.Net.Security;

internal sealed class IncidentLog
{
    private const int MaxRememberedKeys = 4096;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private readonly Action<string> _write;
    private readonly TimeProvider _time;
    private readonly long _perKeyTicks;
    private readonly long _windowTicks;
    private readonly int _linesPerWindow;
    private readonly Dictionary<(GuardIncident Kind, IPAddress Address), long> _lastLogged = [];
    private readonly object _gate = new();
    private long _windowStart;
    private int _windowLines;
    private long _suppressed;

    public IncidentLog(Action<string> write, TimeProvider time, TimeSpan perKeyInterval, int linesPerWindow)
    {
        _write = write;
        _time = time;
        _perKeyTicks = Ticks(perKeyInterval, time);
        _windowTicks = Ticks(Window, time);
        _linesPerWindow = linesPerWindow;
        _windowStart = time.GetTimestamp();
    }

    public void Report(GuardIncident kind, IPAddress address, string detail)
    {
        string? line = null;
        string? summary;
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            summary = RollWindow(now);
            var key = (kind, address);
            var due = !_lastLogged.TryGetValue(key, out var last) || now - last >= _perKeyTicks;
            if (due && _windowLines < _linesPerWindow)
            {
                if (_lastLogged.Count >= MaxRememberedKeys)
                {
                    _lastLogged.Clear();
                }

                _lastLogged[key] = now;
                _windowLines++;
                line = $"[guard] {GuardIncidents.Describe(kind)} from {address}: {detail}";
            }
            else
            {
                _suppressed++;
            }
        }

        Emit(summary);
        Emit(line);
    }

    public void Notice(string text)
    {
        string? summary;
        var written = false;
        lock (_gate)
        {
            summary = RollWindow(_time.GetTimestamp());
            if (_windowLines < _linesPerWindow)
            {
                _windowLines++;
                written = true;
            }
            else
            {
                _suppressed++;
            }
        }

        Emit(summary);
        if (written)
        {
            Emit("[guard] " + text);
        }
    }

    public void Flush()
    {
        string? summary;
        lock (_gate)
        {
            summary = RollWindow(_time.GetTimestamp());
        }

        Emit(summary);
    }

    private string? RollWindow(long now)
    {
        if (now - _windowStart < _windowTicks)
        {
            return null;
        }

        _windowStart = now;
        _windowLines = 0;
        if (_suppressed == 0)
        {
            return null;
        }

        var text = string.Create(CultureInfo.InvariantCulture, $"[guard] {_suppressed} more incidents were not logged");
        _suppressed = 0;
        return text;
    }

    private void Emit(string? line)
    {
        if (line is not null)
        {
            _write(line);
        }
    }

    internal static long Ticks(TimeSpan span, TimeProvider time) =>
        (long)(span.TotalSeconds * time.TimestampFrequency);
}
