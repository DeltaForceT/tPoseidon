namespace tPoseidon.Hosting;

public static class WineClientDisplay
{
    public const string Hive = "HKCU";
    public const string DwordType = "REG_DWORD";

    public static IReadOnlyList<IReadOnlyList<string>> Commands(ClientSetupProfile profile, ClientDisplay display)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ClientDisplay.Validated(display);
        var key = $@"{Hive}\{profile.KeyPath}";
        return ClientDisplayValues.For(display)
            .Select(value => (IReadOnlyList<string>)["reg", "add", key, "/v", value.Name, "/t", DwordType, "/d", value.Data.ToString(System.Globalization.CultureInfo.InvariantCulture), "/f"])
            .ToList();
    }

    public static void Write(IClientProcessRunner runner, string wine, ClientSetupProfile profile, ClientDisplay display)
    {
        ArgumentNullException.ThrowIfNull(runner);
        var quiet = new Dictionary<string, string> { ["WINEDEBUG"] = "-all" };
        foreach (var arguments in Commands(profile, display))
        {
            var result = runner.Run(wine, arguments, quiet);
            if (result.ExitCode != 0)
            {
                throw new HostingException($"Wine could not save the game window settings: {result.Output}", "Check that Wine works (wine --version) and that WINEPREFIX points at a prefix you can write to.", null);
            }
        }
    }
}
