using tPoseidon.Core.Common;

namespace tPoseidon.Core.InterServer;

public sealed record ZoneRoutingLoad(ZoneRoutingTable? Table, IniFile? MasterIni);

public static class ZoneRoutingConfig
{
    public const ushort FallbackZone = 1;

    public static ZoneRoutingLoad Load(string iniPath, string jsonPath, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        ZoneRoutingTable? zoneTable = null;
        IniFile? masterIni = null;
        if (File.Exists(iniPath))
        {
            try
            {
                masterIni = IniFile.LoadFromFile(iniPath);
                zoneTable = ZoneRoutingTable.LoadFromIni(masterIni);
                log($"MasterServer.ini loaded from {iniPath}: {zoneTable.Count} zone routes parsed");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log($"MasterServer.ini load FAILED ({ex.GetType().Name}: {ex.Message}); falling through to JSON");
                masterIni = null;
                zoneTable = null;
            }
        }
        if (zoneTable is null && File.Exists(jsonPath))
        {
            try
            {
                zoneTable = ZoneRoutingTable.LoadFromFile(jsonPath);
                log($"zone-routing JSON loaded from {jsonPath}: {zoneTable.Count} explicit zones, default=idx{zoneTable.DefaultServerIndex}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log($"zone-routing JSON load FAILED ({ex.GetType().Name}: {ex.Message}); falling back to legacy odd/even stub");
            }
        }
        if (zoneTable is null)
        {
            log("no zone-routing config found; using legacy odd/even stub (gserver1=odd, gserver2=even)");
        }
        return new ZoneRoutingLoad(zoneTable, masterIni);
    }

    public static ServerIdentity ResolveGameServer(ZoneRoutingTable? table, ushort zoneId) =>
        table is not null ? table.ResolveGameServer(zoneId) : ZoneRouter.ResolveGameServer(zoneId);

    public static ServerIdentity ResolveLoginServer(ZoneRoutingTable? table, ushort savedZone) =>
        ResolveGameServer(table, savedZone == 0 ? FallbackZone : savedZone);
}
