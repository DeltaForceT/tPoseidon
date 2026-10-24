using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed class GuildNameBook
{
    private readonly Encoding _text;

    public GuildNameBook(SystemServerLink link, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(link);
        _text = text ?? throw new ArgumentNullException(nameof(text));
        link.OrderReceived += ApplyAsync;
    }

    public event Func<ulong, string, string, Task>? Changed;

    public Task ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.OrderType != SystemLinkFrames.OrderGuildName || Changed is not { } changed) return Task.CompletedTask;
        var (guildName, alias) = GuildFrames.ReadNameOrder(order.Data, _text);
        return changed(order.CharacterId, guildName, alias);
    }
}
