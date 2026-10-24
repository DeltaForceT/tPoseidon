using tPoseidon.Core.Data;

namespace tPoseidon.DrillEgg.Config;

public sealed class DrillEggSettings
{
    public int Port { get; init; } = 8080;

    public string Listen { get; init; } = ListenAddresses.Localhost;

    public bool OpenBrowser { get; init; } = true;

    public FileSettings Files { get; init; } = new();

    public ConnectorSettings Players { get; init; } = new() { Connector = ConnectorNames.Local };

    public ConnectorSettings Coupons { get; init; } = new() { Connector = ConnectorNames.Free };

    public ConnectorSettings Delivery { get; init; } = new() { Connector = ConnectorNames.Local };

    public IReadOnlyDictionary<string, DatabaseSettings> Databases { get; init; } = new Dictionary<string, DatabaseSettings>();

    public TposeidonSettings? Tposeidon { get; init; }

    public IReadOnlyList<PrizeSettings> Prizes { get; init; } = [];

    public DeepDrillSettings DeepDrill { get; init; } = new();

    public IReadOnlyDictionary<string, AreaSettings> Areas { get; init; } = new Dictionary<string, AreaSettings>();
}

public sealed class FileSettings
{
    public string Swf { get; init; } = "myshop.swf";

    public string Art { get; init; } = "art";

    public string Pictures { get; init; } = "pictures";

    public string Data { get; init; } = "drill-egg.db";
}

public sealed class ConnectorSettings
{
    public string Connector { get; init; } = string.Empty;

    public string? Mode { get; init; }

    public string? Name { get; init; }

    public int? StartingCoupons { get; init; }

    public string? Database { get; init; }

    public string? SignIn { get; init; }

    public string? Query { get; init; }

    public string? Count { get; init; }

    public string? Spend { get; init; }

    public string? Deliver { get; init; }

    public Uri? Url { get; init; }

    public Uri? CountUrl { get; init; }

    public Uri? SpendUrl { get; init; }

    public string? SecretHeader { get; init; }

    public string? Secret { get; init; }

    public int? TimeoutSeconds { get; init; }

    public int? RetrySeconds { get; init; }
}

public sealed class DatabaseSettings
{
    public string Provider { get; init; } = string.Empty;

    public string Connection { get; init; } = string.Empty;
}

public sealed class TposeidonSettings
{
    public string Connection { get; init; } = string.Empty;

    public string? Schema { get; init; }
}

public sealed class PrizeSettings
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int Weight { get; init; }

    public int? DeepWeight { get; init; }

    public bool Rare { get; init; }

    public string? Picture { get; init; }

    public short Count { get; init; } = 1;

    public byte ItemClass { get; init; } = ChargedItemRules.CommonClass;

    public short UseHours { get; init; }

    public byte ItemFlag { get; init; }
}

public sealed class DeepDrillSettings
{
    public int RareBoost { get; init; } = 3;
}

public sealed class AreaSettings
{
    public IReadOnlyList<PrizeSettings> Prizes { get; init; } = [];
}

public static class ConnectorNames
{
    public const string Local = "local";
    public const string Free = "free";
    public const string Sql = "sql";
    public const string Http = "http";
    public const string Tposeidon = "tposeidon";
}

public static class ListenAddresses
{
    public const string Localhost = "localhost";
    public const string Any = "any";
}
