namespace tPoseidon.Core.Net.World;

public sealed record WarpTicketUse(string Notice, System.Text.Encoding Text, Func<CancellationToken, Task> Warp);
