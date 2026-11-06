using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class SystemNoticeHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.CcmdSysMegaphone };

    private readonly IMegaphoneQueueStore _queue;
    private readonly Func<string> _islandName;
    private readonly Action<string> _log;

    public SystemNoticeHandler(IMegaphoneQueueStore queue, Func<string> islandName, Action<string> log)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _islandName = islandName ?? throw new ArgumentNullException(nameof(islandName));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var notice = SystemNoticeMessages.Decode(frame.Body, ClientText.Encoding);
        var line = SystemNoticeMessages.Line(notice, _islandName(), ClientText.Encoding);
        try
        {
            var waiting = _queue.EnqueueNotice(line);
            _log($"system notice queued behind {waiting}: {line}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"system notice not saved: {ex.GetType().Name}: {ex.Message}");
        }
        return ValueTask.CompletedTask;
    }
}
