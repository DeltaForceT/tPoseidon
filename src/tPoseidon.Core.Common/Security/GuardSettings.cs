using System.Globalization;
using System.Text.Json;

namespace tPoseidon.Core.Common.Security;

public sealed class GuardSettings
{
    public const string SettingsVariable = "MT_SECURITY_SETTINGS";
    public const string ServerNameVariable = "MT_SECURITY_NAME";
    public const string StateDirectoryVariable = "MT_SECURITY_DIR";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public bool Enabled { get; set; } = true;

    public List<string> TrustedAddresses { get; set; } = [];

    public int MaxConnectionsPerAddress { get; set; } = 32;

    public int NewConnectionsPerAddressBurst { get; set; } = 20;

    public double NewConnectionsPerAddressPerSecond { get; set; } = 1;

    public int MaxConnections { get; set; } = 4000;

    public int MaxPendingHandshakes { get; set; } = 512;

    public int HandshakeTimeoutSeconds { get; set; } = 30;

    public int LoginTimeoutSeconds { get; set; } = 120;

    public int IdleTimeoutSeconds { get; set; } = 600;

    public int PartialFrameTimeoutSeconds { get; set; } = 15;

    public int MaxFrameBytes { get; set; } = 16384;

    public int PacketBurst { get; set; } = 400;

    public double PacketsPerSecond { get; set; } = 60;

    public int OpcodeBurst { get; set; } = 60;

    public double OpcodePacketsPerSecond { get; set; } = 10;

    public List<OpcodeAllowance> OpcodeAllowances { get; set; } = OpcodeAllowance.Defaults();

    public int LoginFailuresBeforeDelay { get; set; } = 3;

    public int LoginDelayMaxSeconds { get; set; } = 16;

    public int AccountLockoutFailures { get; set; } = 10;

    public int AddressLockoutFailures { get; set; } = 30;

    public int LoginFailureWindowMinutes { get; set; } = 15;

    public int LockoutMinutes { get; set; } = 15;

    public int StrikesBeforeBan { get; set; } = 10;

    public int StrikeWindowSeconds { get; set; } = 60;

    public int BanMinutes { get; set; } = 5;

    public int BanMaxMinutes { get; set; } = 1440;

    public int BanForgetHours { get; set; } = 24;

    public int RequestBurst { get; set; } = 120;

    public double RequestsPerSecond { get; set; } = 20;

    public int MaxRequestBodyBytes { get; set; } = 1048576;

    public int IncidentLogIntervalSeconds { get; set; } = 30;

    public int IncidentLogLinesPerTenSeconds { get; set; } = 20;

    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        Positive(problems, nameof(MaxConnectionsPerAddress), MaxConnectionsPerAddress);
        Positive(problems, nameof(NewConnectionsPerAddressBurst), NewConnectionsPerAddressBurst);
        Positive(problems, nameof(NewConnectionsPerAddressPerSecond), NewConnectionsPerAddressPerSecond);
        Positive(problems, nameof(MaxConnections), MaxConnections);
        Positive(problems, nameof(MaxPendingHandshakes), MaxPendingHandshakes);
        Positive(problems, nameof(HandshakeTimeoutSeconds), HandshakeTimeoutSeconds);
        Positive(problems, nameof(LoginTimeoutSeconds), LoginTimeoutSeconds);
        NotNegative(problems, nameof(IdleTimeoutSeconds), IdleTimeoutSeconds);
        Positive(problems, nameof(PartialFrameTimeoutSeconds), PartialFrameTimeoutSeconds);
        Positive(problems, nameof(PacketBurst), PacketBurst);
        Positive(problems, nameof(PacketsPerSecond), PacketsPerSecond);
        Positive(problems, nameof(OpcodeBurst), OpcodeBurst);
        Positive(problems, nameof(OpcodePacketsPerSecond), OpcodePacketsPerSecond);
        NotNegative(problems, nameof(LoginFailuresBeforeDelay), LoginFailuresBeforeDelay);
        NotNegative(problems, nameof(LoginDelayMaxSeconds), LoginDelayMaxSeconds);
        Positive(problems, nameof(AccountLockoutFailures), AccountLockoutFailures);
        Positive(problems, nameof(AddressLockoutFailures), AddressLockoutFailures);
        Positive(problems, nameof(LoginFailureWindowMinutes), LoginFailureWindowMinutes);
        Positive(problems, nameof(LockoutMinutes), LockoutMinutes);
        Positive(problems, nameof(StrikesBeforeBan), StrikesBeforeBan);
        Positive(problems, nameof(StrikeWindowSeconds), StrikeWindowSeconds);
        Positive(problems, nameof(BanMinutes), BanMinutes);
        Positive(problems, nameof(BanForgetHours), BanForgetHours);
        Positive(problems, nameof(RequestBurst), RequestBurst);
        Positive(problems, nameof(RequestsPerSecond), RequestsPerSecond);
        Positive(problems, nameof(MaxRequestBodyBytes), MaxRequestBodyBytes);
        if (BanMaxMinutes < BanMinutes)
        {
            problems.Add($"{Camel(nameof(BanMaxMinutes))} must be at least {Camel(nameof(BanMinutes))}.");
        }
        if (MaxFrameBytes < MinimumFrameBytes || MaxFrameBytes > ushort.MaxValue)
        {
            problems.Add($"{Camel(nameof(MaxFrameBytes))} must be between {MinimumFrameBytes} and {ushort.MaxValue}.");
        }

        foreach (var allowance in OpcodeAllowances ?? [])
        {
            if (allowance.Problem() is { } problem)
            {
                problems.Add(problem);
            }
        }

        Positive(problems, nameof(IncidentLogIntervalSeconds), IncidentLogIntervalSeconds);
        Positive(problems, nameof(IncidentLogLinesPerTenSeconds), IncidentLogLinesPerTenSeconds);
        foreach (var address in TrustedAddresses ?? [])
        {
            if (!AddressRange.TryParse(address, out _))
            {
                problems.Add($"trustedAddresses entry '{address}' is not an address or a range like 10.0.0.0/8.");
            }
        }

        return problems;
    }

    public const int MinimumFrameBytes = 64;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static GuardSettings FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<GuardSettings>(json, JsonOptions)
            ?? throw new JsonException("security settings are empty");
    }

    public static GuardSettings FromEnvironment(Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var raw = Environment.GetEnvironmentVariable(SettingsVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new GuardSettings();
        }

        GuardSettings parsed;
        try
        {
            parsed = FromJson(raw);
        }
        catch (JsonException ex)
        {
            report($"{SettingsVariable} is not valid security settings ({ex.Message}); using the defaults");
            return new GuardSettings();
        }

        var problems = parsed.Problems();
        if (problems.Count == 0)
        {
            return parsed;
        }

        report($"security settings have problems ({string.Join(" ", problems)}); using the defaults");
        return new GuardSettings();
    }

    public static string? StateDirectoryFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(StateDirectoryVariable);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    public static string? ServerNameFromEnvironment()
    {
        var raw = Environment.GetEnvironmentVariable(ServerNameVariable);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static void Positive(List<string> problems, string name, double value)
    {
        if (!(value > 0))
        {
            problems.Add($"{Camel(name)} must be above zero.");
        }
    }

    private static void NotNegative(List<string> problems, string name, double value)
    {
        if (value < 0)
        {
            problems.Add($"{Camel(name)} must not be negative.");
        }
    }

    private static string Camel(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"{char.ToLowerInvariant(name[0])}{name[1..]}");
}
