namespace tPoseidon.Core.Game.Scripting.Host;

public interface IScriptSessionHost : IScriptHost
{
    Task FlushAsync(CancellationToken ct);

    bool TakeCloseRequest();
}
