using System.Buffers;
using System.Globalization;
using System.Text;

namespace tPoseidon.Core.Common;

public sealed class IniFile
{
    public const string DefaultSection = "";

    private static readonly SearchValues<char> KeyArgSeparators =
        SearchValues.Create(" \t");

    private readonly Dictionary<string, IniSection> _sections =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, IniSection> Sections => _sections;

    public IniSection Section(string name = DefaultSection)
    {
        if (!_sections.TryGetValue(name, out var section))
        {
            section = new IniSection(name);
            _sections[name] = section;
        }
        return section;
    }

    public bool TryGetString(string key, out string value, string section = DefaultSection)
    {
        if (_sections.TryGetValue(section, out var s) && s.TryGetFirst(key, out value!))
        {
            return true;
        }
        value = string.Empty;
        return false;
    }

    public string? GetString(string key, string section = DefaultSection) =>
        TryGetString(key, out var v, section) ? v : null;

    public int GetInt(string key, int fallback, string section = DefaultSection) =>
        TryGetString(key, out var v, section) &&
        int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i : fallback;

    public bool GetBool(string key, bool fallback, string section = DefaultSection)
    {
        if (!TryGetString(key, out var v, section)) return fallback;
        if (v is "1" or "true" or "True" or "yes" or "on") return true;
        if (v is "0" or "false" or "False" or "no" or "off") return false;
        return fallback;
    }

    public static IniFile LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = File.ReadAllBytes(path);
        var latin1 = Encoding.GetEncoding(28591);
        var text = latin1.GetString(bytes);
        return Parse(text);
    }

    public static IniFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var ini = new IniFile();
        var current = ini.Section(DefaultSection);

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;
            if (line[0] is ';' or '#') continue;

            if (line[0] == '[')
            {
                var end = line.IndexOf(']');
                if (end < 0) continue;
                var name = line.Substring(1, end - 1).Trim();
                current = ini.Section(name);
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 0) continue;
            var keyRegion = line.Substring(0, eq).Trim();
            var value = line.Substring(eq + 1);
            var inlineComment = value.IndexOf(';');
            if (inlineComment >= 0) value = value.Substring(0, inlineComment);
            value = value.Trim();
            if (keyRegion.Length == 0) continue;

            string key;
            var firstSpace = keyRegion.AsSpan().IndexOfAny(KeyArgSeparators);
            if (firstSpace > 0)
            {
                key = keyRegion.Substring(0, firstSpace);
                var argument = keyRegion.Substring(firstSpace + 1).Trim();
                value = argument.Length > 0 ? argument + " " + value : value;
            }
            else
            {
                key = keyRegion;
            }
            current.Add(key, value);
        }

        return ini;
    }
}

public sealed class IniSection
{
    private readonly Dictionary<string, List<string>> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }

    public IniSection(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public void Add(string key, string value)
    {
        if (!_values.TryGetValue(key, out var list))
        {
            list = new List<string>();
            _values[key] = list;
        }
        list.Add(value);
    }

    public bool TryGetFirst(string key, out string value)
    {
        if (_values.TryGetValue(key, out var list) && list.Count > 0)
        {
            value = list[0];
            return true;
        }
        value = string.Empty;
        return false;
    }

    public IReadOnlyList<string> GetAll(string key) =>
        _values.TryGetValue(key, out var list)
            ? list
            : Array.Empty<string>();
}

public static class SiblingIdParser
{
    public readonly record struct SiblingId(string Name, string Password);

    public static SiblingId Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var trimmed = raw.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon < 0) return new SiblingId(trimmed, string.Empty);
        var name = trimmed.Substring(0, colon).Trim();
        var pw = trimmed.Substring(colon + 1).Trim();
        return new SiblingId(name, pw);
    }
}

public static class IniZoneRouteParser
{
    private static readonly char[] Separators = { ' ', '\t' };

    public readonly record struct ZoneRoute(int ZoneId, string ServerName);

    public static IReadOnlyList<ZoneRoute> ParseZoneRoutes(IniSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var result = new List<ZoneRoute>();
        foreach (var raw in section.GetAll("zone"))
        {
            var value = raw.Trim();
            if (value.Length == 0) continue;
            var parts = value.Split(Separators, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1) continue;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var zoneId)) continue;
            var server = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            if (server.Length == 0) continue;
            result.Add(new ZoneRoute(zoneId, server));
        }
        return result;
    }
}
