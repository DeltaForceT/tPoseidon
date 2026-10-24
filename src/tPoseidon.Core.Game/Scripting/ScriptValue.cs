using System.Globalization;

namespace tPoseidon.Core.Game.Scripting;

public readonly struct ScriptValue : IEquatable<ScriptValue>
{
    public const int MaxTextLength = 2048;

    private readonly string? _text;

    private ScriptValue(long number, string? text)
    {
        Number = number;
        _text = text;
    }

    public long Number { get; }

    public bool IsText => _text is not null;

    public string Text => _text ?? Number.ToString(CultureInfo.InvariantCulture);

    public bool IsTrue => _text is null ? Number != 0 : _text.Length != 0;

    public static ScriptValue Zero { get; } = new(0, null);

    public static ScriptValue FromNumber(long number) => new(number, null);

    public static ScriptValue FromBool(bool value) => new(value ? 1 : 0, null);

    public static ScriptValue FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ScriptValue(0, text.Length > MaxTextLength ? text[..MaxTextLength] : text);
    }

    public bool Equals(ScriptValue other) => _text is null && other._text is null
        ? Number == other.Number
        : string.Equals(Text, other.Text, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is ScriptValue other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text);

    public static bool operator ==(ScriptValue left, ScriptValue right) => left.Equals(right);

    public static bool operator !=(ScriptValue left, ScriptValue right) => !left.Equals(right);

    public override string ToString() => Text;
}
