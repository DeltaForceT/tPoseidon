using System.Globalization;
using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Prizes;

namespace tPoseidon.DrillEgg.Config;

internal static class SettingsValidator
{
    public const string SinglePlayerMode = "single";
    public const string NamedPlayerMode = "name";
    public const string PasswordSignIn = "password";
    public const string TokenSignIn = "token";
    public const int MaxPort = 65535;

    private static readonly string[] PlayerConnectors = [ConnectorNames.Local, ConnectorNames.Sql, ConnectorNames.Http, ConnectorNames.Tposeidon];
    private static readonly string[] CouponConnectors = [ConnectorNames.Free, ConnectorNames.Local, ConnectorNames.Sql, ConnectorNames.Http, ConnectorNames.Tposeidon];
    private static readonly string[] DeliveryConnectors = [ConnectorNames.Local, ConnectorNames.Sql, ConnectorNames.Http, ConnectorNames.Tposeidon];

    public static IReadOnlyList<string> Problems(DrillEggSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var problems = new List<string>();
        if (settings.Port is < 1 or > MaxPort) problems.Add($"port must be between 1 and {MaxPort} (got {settings.Port}).");
        if (!IsListen(settings.Listen)) problems.Add($"listen must be \"{ListenAddresses.Localhost}\", \"{ListenAddresses.Any}\" or an IP address of this machine (got \"{settings.Listen}\").");
        CheckFiles(settings.Files, problems);
        foreach (var (name, database) in settings.Databases)
        {
            if (!SqlProviders.IsKnown(database.Provider)) problems.Add($"databases.{name}.provider must be one of {string.Join(", ", SqlProviders.Names)} (got \"{database.Provider}\").");
            if (string.IsNullOrWhiteSpace(database.Connection)) problems.Add($"databases.{name}.connection is empty.");
        }
        CheckPlayers(settings, problems);
        CheckCoupons(settings, problems);
        CheckDelivery(settings, problems);
        if (Uses(settings, ConnectorNames.Tposeidon) && string.IsNullOrWhiteSpace(settings.Tposeidon?.Connection))
        {
            problems.Add("a tposeidon connector is chosen, so tposeidon.connection must hold the emulator's PostgreSQL connection.");
        }
        CheckPrizes(settings, problems);
        return problems;
    }

    public static bool UsesStorePools(DrillEggSettings settings) => Is(settings.Coupons, ConnectorNames.Tposeidon);

    private static void CheckFiles(FileSettings files, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(files.Swf)) problems.Add("files.swf is empty.");
        if (string.IsNullOrWhiteSpace(files.Art)) problems.Add("files.art is empty.");
        if (string.IsNullOrWhiteSpace(files.Pictures)) problems.Add("files.pictures is empty.");
        if (string.IsNullOrWhiteSpace(files.Data)) problems.Add("files.data is empty.");
    }

    private static void CheckPlayers(DrillEggSettings settings, List<string> problems)
    {
        var players = settings.Players;
        if (!Known(players, PlayerConnectors, "players", problems)) return;
        switch (players.Connector)
        {
            case ConnectorNames.Local:
                if (players.Mode is not (null or SinglePlayerMode or NamedPlayerMode)) problems.Add($"players.mode must be \"{SinglePlayerMode}\" or \"{NamedPlayerMode}\" (got \"{players.Mode}\").");
                if (players.Name is { } name && !PlayerNames.IsValid(name)) problems.Add($"players.name \"{name}\" is not a valid player name ({PlayerNames.Rule}).");
                break;
            case ConnectorNames.Sql:
                CheckSignIn(players, problems);
                CheckDatabase(settings, players, "players", problems);
                Require(players.Query, "players.query", problems);
                break;
            case ConnectorNames.Http:
                CheckSignIn(players, problems);
                RequireUrl(players.Url, "players.url", problems);
                CheckHttp(players, "players", problems);
                break;
        }
    }

    private static void CheckCoupons(DrillEggSettings settings, List<string> problems)
    {
        var coupons = settings.Coupons;
        if (!Known(coupons, CouponConnectors, "coupons", problems)) return;
        switch (coupons.Connector)
        {
            case ConnectorNames.Local:
                if (coupons.StartingCoupons is < 0) problems.Add("coupons.startingCoupons cannot be negative.");
                break;
            case ConnectorNames.Sql:
                CheckDatabase(settings, coupons, "coupons", problems);
                Require(coupons.Count, "coupons.count", problems);
                Require(coupons.Spend, "coupons.spend", problems);
                break;
            case ConnectorNames.Http:
                RequireUrl(coupons.CountUrl, "coupons.countUrl", problems);
                RequireUrl(coupons.SpendUrl, "coupons.spendUrl", problems);
                CheckHttp(coupons, "coupons", problems);
                break;
            case ConnectorNames.Tposeidon:
                if (!Is(settings.Delivery, ConnectorNames.Tposeidon)) problems.Add("tposeidon coupons are spent in the same transaction as the tposeidon prize delivery, so delivery.connector must be \"tposeidon\" too.");
                if (!Is(settings.Players, ConnectorNames.Tposeidon)) problems.Add("tposeidon coupons belong to emulator accounts, so players.connector must be \"tposeidon\" too.");
                break;
        }
    }

    private static void CheckDelivery(DrillEggSettings settings, List<string> problems)
    {
        var delivery = settings.Delivery;
        if (!Known(delivery, DeliveryConnectors, "delivery", problems)) return;
        if (delivery.RetrySeconds is <= 0) problems.Add("delivery.retrySeconds must be above 0.");
        switch (delivery.Connector)
        {
            case ConnectorNames.Sql:
                CheckDatabase(settings, delivery, "delivery", problems);
                Require(delivery.Deliver, "delivery.deliver", problems);
                break;
            case ConnectorNames.Http:
                RequireUrl(delivery.Url, "delivery.url", problems);
                CheckHttp(delivery, "delivery", problems);
                break;
            case ConnectorNames.Tposeidon:
                if (!Is(settings.Players, ConnectorNames.Tposeidon) && !Is(settings.Players, ConnectorNames.Sql) && !Is(settings.Players, ConnectorNames.Http))
                {
                    problems.Add("tposeidon delivery needs the player's emulator account id, so players.connector must be \"tposeidon\", \"sql\" or \"http\".");
                }
                break;
        }
    }

    private static void CheckPrizes(DrillEggSettings settings, List<string> problems)
    {
        if (settings.DeepDrill.RareBoost < 1) problems.Add("deepDrill.rareBoost must be 1 or more.");
        var lists = new List<(string Where, IReadOnlyList<PrizeSettings> Prizes)> { ("prizes", settings.Prizes) };
        foreach (var (key, area) in settings.Areas)
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || !GachaRules.IsArea(number))
            {
                problems.Add($"areas.\"{key}\" is not a dig site; use {GachaRules.FirstArea} to {GachaRules.LastArea}.");
                continue;
            }
            lists.Add(($"areas.{key}.prizes", area.Prizes));
        }
        if (settings.Prizes.Count == 0 && !UsesStorePools(settings)) problems.Add("prizes is empty; list at least one prize.");
        var names = new Dictionary<int, string>();
        foreach (var (where, prizes) in lists)
        {
            var seen = new HashSet<int>();
            long normal = 0;
            long deep = 0;
            foreach (var prize in prizes)
            {
                var label = $"{where}: prize {prize.Id.ToString(CultureInfo.InvariantCulture)}";
                if (prize.Id <= 0) problems.Add($"{label}: id must be above 0.");
                if (!seen.Add(prize.Id)) problems.Add($"{label} is listed twice.");
                if (string.IsNullOrWhiteSpace(prize.Name)) problems.Add($"{label}: name is empty.");
                else if (names.TryGetValue(prize.Id, out var other) && !string.Equals(other, prize.Name, StringComparison.Ordinal)) problems.Add($"{label} is called \"{prize.Name}\" here and \"{other}\" elsewhere.");
                else names[prize.Id] = prize.Name;
                if (prize.Weight <= 0) problems.Add($"{label}: weight must be above 0.");
                if (prize.DeepWeight is <= 0) problems.Add($"{label}: deepWeight must be above 0.");
                if (prize.Count <= 0) problems.Add($"{label}: count must be above 0.");
                if (prize.UseHours < 0) problems.Add($"{label}: useHours cannot be negative.");
                if (prize.Picture is { } picture && !PrizePictureNames.IsPlainFileName(picture)) problems.Add($"{label}: picture must be a file name inside the pictures folder (got \"{picture}\").");
                normal += Math.Max(prize.Weight, 0);
                deep += Math.Max(PrizeCatalog.DeepWeightOf(prize, settings.DeepDrill.RareBoost), 0);
            }
            if (normal > int.MaxValue || deep > int.MaxValue) problems.Add($"{where}: the weights add up to more than {int.MaxValue.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void CheckSignIn(ConnectorSettings players, List<string> problems)
    {
        if (players.SignIn is not (null or PasswordSignIn or TokenSignIn)) problems.Add($"players.signIn must be \"{PasswordSignIn}\" or \"{TokenSignIn}\" (got \"{players.SignIn}\").");
    }

    private static void CheckDatabase(DrillEggSettings settings, ConnectorSettings connector, string section, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(connector.Database)) problems.Add($"{section}.database must name one of the databases.");
        else if (!settings.Databases.ContainsKey(connector.Database)) problems.Add($"{section}.database \"{connector.Database}\" is not listed under databases.");
    }

    private static void CheckHttp(ConnectorSettings connector, string section, List<string> problems)
    {
        if (connector.TimeoutSeconds is <= 0) problems.Add($"{section}.timeoutSeconds must be above 0.");
        if (connector.SecretHeader is { } header && (header.Length == 0 || header.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))) problems.Add($"{section}.secretHeader must be letters, digits and dashes (got \"{header}\").");
        if (connector.Secret is not null && connector.Secret.Length == 0) problems.Add($"{section}.secret is empty; remove it or set a value.");
    }

    private static bool Known(ConnectorSettings connector, string[] allowed, string section, List<string> problems)
    {
        if (allowed.Contains(connector.Connector, StringComparer.Ordinal)) return true;
        problems.Add($"{section}.connector must be one of {string.Join(", ", allowed)} (got \"{connector.Connector}\").");
        return false;
    }

    private static void Require(string? value, string name, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(value)) problems.Add($"{name} is missing.");
    }

    private static void RequireUrl(Uri? value, string name, List<string> problems)
    {
        if (value is null) problems.Add($"{name} is missing.");
        else if (!value.IsAbsoluteUri || (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps)) problems.Add($"{name} must be a full http:// or https:// address (got \"{value}\").");
    }

    private static bool IsListen(string listen) =>
        listen is ListenAddresses.Localhost or ListenAddresses.Any || IPAddress.TryParse(listen, out _);

    private static bool Uses(DrillEggSettings settings, string connector) =>
        Is(settings.Players, connector) || Is(settings.Coupons, connector) || Is(settings.Delivery, connector);

    private static bool Is(ConnectorSettings settings, string connector) =>
        string.Equals(settings.Connector, connector, StringComparison.Ordinal);
}
