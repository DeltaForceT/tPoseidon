using System.Text;
using tPoseidon.Core.Game.Scripting.Vm;

namespace tPoseidon.Core.Net.Dialogs;

public static class ScriptDialogMarkup
{
    public const string LineBreak = "<br/>";
    public const char Separator = '\\';
    public const string NextLabel = "Next";
    public const string CloseLabel = "Close";
    public const string ExitFunction = "Exit";
    public const int ContinueIndex = 1;
    public const int MaximumMenuOptions = 20;

    public static string Render(ScriptPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var text = new StringBuilder();
        text.Append(string.Join(LineBreak, page.Lines.Select(Clean)));
        switch (page.Kind)
        {
            case PageKind.Next:
                AppendLink(text, NextLabel, ContinueIndex);
                break;
            case PageKind.Menu:
                for (var i = 0; i < page.Options.Count; i++) AppendLink(text, page.Options[i], i + 1);
                break;
            default:
                text.Append(Separator).Append(CloseLabel).Append(Separator).Append(ExitFunction);
                break;
        }
        return text.ToString();
    }

    private static void AppendLink(StringBuilder text, string label, int index)
    {
        text.Append(Separator).Append(Clean(label)).Append(Separator).Append(index);
    }

    private static string Clean(string text)
    {
        var cleaned = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c == Separator || c == '\r') continue;
            cleaned.Append(c is >= ' ' and <= '~' ? c : '?');
        }
        return cleaned.ToString();
    }
}
