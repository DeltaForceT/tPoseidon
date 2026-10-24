namespace tPoseidon.Core.Net.Security;

public enum GuardIncident
{
    ConnectionRate,
    ConnectionLimit,
    HandshakeTimeout,
    LoginTimeout,
    IdleTimeout,
    PartialFrame,
    OversizedFrame,
    MalformedFrame,
    PacketFlood,
    LoginFailure,
    LoginLockout,
    RequestFlood,
}

public enum GuardRefusal
{
    None,
    Banned,
    ServerFull,
    TooManyPending,
    AddressLimit,
    AddressRate,
}

public static class GuardIncidents
{
    public static int Strikes(GuardIncident incident) => incident switch
    {
        GuardIncident.ConnectionRate => 1,
        GuardIncident.ConnectionLimit => 1,
        GuardIncident.HandshakeTimeout => 1,
        GuardIncident.LoginTimeout => 1,
        GuardIncident.IdleTimeout => 0,
        GuardIncident.PartialFrame => 2,
        GuardIncident.OversizedFrame => 5,
        GuardIncident.MalformedFrame => 5,
        GuardIncident.PacketFlood => 5,
        GuardIncident.LoginFailure => 0,
        GuardIncident.LoginLockout => 2,
        GuardIncident.RequestFlood => 1,
        _ => 1,
    };

    public static string Describe(GuardIncident incident) => incident switch
    {
        GuardIncident.ConnectionRate => "connection rate",
        GuardIncident.ConnectionLimit => "too many connections",
        GuardIncident.HandshakeTimeout => "no valid frame in time",
        GuardIncident.LoginTimeout => "no login in time",
        GuardIncident.IdleTimeout => "idle",
        GuardIncident.PartialFrame => "partial frame held open",
        GuardIncident.OversizedFrame => "oversized frame",
        GuardIncident.MalformedFrame => "malformed frame",
        GuardIncident.PacketFlood => "packet flood",
        GuardIncident.LoginFailure => "failed login",
        GuardIncident.LoginLockout => "login while locked out",
        GuardIncident.RequestFlood => "web request flood",
        _ => incident.ToString(),
    };

    public static string Key(GuardIncident incident) => incident switch
    {
        GuardIncident.ConnectionRate => "connectionRate",
        GuardIncident.ConnectionLimit => "connectionLimit",
        GuardIncident.HandshakeTimeout => "handshakeTimeout",
        GuardIncident.LoginTimeout => "loginTimeout",
        GuardIncident.IdleTimeout => "idleTimeout",
        GuardIncident.PartialFrame => "partialFrame",
        GuardIncident.OversizedFrame => "oversizedFrame",
        GuardIncident.MalformedFrame => "malformedFrame",
        GuardIncident.PacketFlood => "packetFlood",
        GuardIncident.LoginFailure => "loginFailure",
        GuardIncident.LoginLockout => "loginLockout",
        GuardIncident.RequestFlood => "requestFlood",
        _ => incident.ToString(),
    };
}
