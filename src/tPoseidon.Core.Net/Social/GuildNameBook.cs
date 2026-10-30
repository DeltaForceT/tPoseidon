using System.Text;
using tPoseidon.Core.Data;

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

    public event Func<ulong, GuildTag, Task>? Changed;

    public Task ApplyAsync(OfficerOrder order, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.OrderType != SystemLinkFrames.OrderGuildName || Changed is not { } changed) return Task.CompletedTask;
        return changed(order.CharacterId, GuildFrames.ReadNameOrder(order.Data, _text));
    }
}
