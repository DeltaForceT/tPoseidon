using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher;

public sealed class MasterContext
{
    public MasterContext(
        MasterServerHost host,
        Func<ushort, ServerIdentity> resolveGameServer,
        DateTimeOffset bootTime,
        Action<string> log,
        IReadOnlyDictionary<string, string> expectedSiblingPasswords,
        ZoneRoutingTable? zoneTable = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(resolveGameServer);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(expectedSiblingPasswords);
        Host = host;
        ResolveGameServer = resolveGameServer;
        BootTime = bootTime;
        Log = log;
        ExpectedSiblingPasswords = expectedSiblingPasswords;
        ZoneTable = zoneTable;
    }

    public MasterServerHost Host { get; }
    public Func<ushort, ServerIdentity> ResolveGameServer { get; }
    public DateTimeOffset BootTime { get; }
    public Action<string> Log { get; }
    public IReadOnlyDictionary<string, string> ExpectedSiblingPasswords { get; }
    public ZoneRoutingTable? ZoneTable { get; }
}
