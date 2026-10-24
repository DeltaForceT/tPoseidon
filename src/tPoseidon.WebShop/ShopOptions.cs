using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.WebShop;

public sealed record ShopOptions(string Urls, string LibConfigPath, CashShopPolicy Cash)
{
    public const string UrlsVariable = "MT_WEBSHOP_URLS";
    public const string LibConfigVariable = "MT_LIBCONFIG_PATH";
    public const string CashModeVariable = "MT_CASH_MODE";
    public const string CashStartVariable = "MT_CASH_START";
    public const int WorldCode = 1;
    public const string DefaultUrls = "http://127.0.0.1:9990";

    public static ShopOptions FromEnvironment()
    {
        var urls = Environment.GetEnvironmentVariable(UrlsVariable);
        var libconfig = Environment.GetEnvironmentVariable(LibConfigVariable)
            ?? FirstExisting(
                RepoRoot.Data("variants", "Kr2014", "libconfig.dat"),
                RepoRoot.Data("variants", "Kr2014", "libconfig_eng.dat"),
                RepoRoot.Data("krn-2014.01.28", "libconfig_decrypted.xml"));
        return new ShopOptions(
            string.IsNullOrWhiteSpace(urls) ? DefaultUrls : urls.Trim(),
            libconfig,
            CashFromEnvironment());
    }

    public static CashShopPolicy CashFromEnvironment()
    {
        var mode = Environment.GetEnvironmentVariable(CashModeVariable)?.Trim().ToUpperInvariant() switch
        {
            null or "" or "FREE" => CashMode.Free,
            "BALANCE" => CashMode.Balance,
            "DISABLED" => CashMode.Disabled,
            var other => throw new InvalidOperationException($"{CashModeVariable} must be free, balance or disabled (got '{other}')."),
        };
        var start = long.TryParse(Environment.GetEnvironmentVariable(CashStartVariable), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var amount) && amount >= 0 ? amount : 0;
        return new CashShopPolicy(mode, WorldCode, start);
    }

    private static string FirstExisting(params string[] paths) => paths.FirstOrDefault(File.Exists) ?? paths[^1];
}
