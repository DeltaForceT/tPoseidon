using tPoseidon.Core.Common.Security;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class SecuritySettingsTests
{
    [Fact]
    public void SavedSettings_ContainTheSecuritySectionWithItsDefaults()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);

        new HostSettings().Save(paths);

        var json = File.ReadAllText(paths.SettingsFile);
        Assert.Contains("\"security\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"maxConnectionsPerAddress\": 32", json, StringComparison.Ordinal);
        Assert.Contains("\"opcode\": \"0x0018\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangedSecuritySettings_SurviveSaveAndLoad()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings();
        settings.Security.MaxConnectionsPerAddress = 64;
        settings.Security.TrustedAddresses = ["10.20.0.0/16"];
        settings.Save(paths);

        var loaded = HostSettings.Load(paths)!;

        Assert.Equal(64, loaded.Security.MaxConnectionsPerAddress);
        Assert.Equal(["10.20.0.0/16"], loaded.Security.TrustedAddresses);
    }

    [Fact]
    public void OlderSettingsFile_WithoutASecuritySection_GetsTheDefaults()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        File.WriteAllText(paths.SettingsFile, "{ \"formatVersion\": 1, \"clientFolder\": \"c\" }");

        var loaded = HostSettings.Load(paths)!;

        Assert.Equal(new GuardSettings().ToJson(), loaded.Security.ToJson());
    }

    [Fact]
    public void NullSecuritySection_GetsTheDefaults()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        File.WriteAllText(paths.SettingsFile, "{ \"security\": null }");

        Assert.True(HostSettings.Load(paths)!.Security.Enabled);
    }

    [Fact]
    public void BrokenSecuritySettings_StopTheLoadWithTheReason()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        File.WriteAllText(paths.SettingsFile, "{ \"security\": { \"maxConnections\": 0 } }");

        var error = Assert.Throws<HostingException>(() => HostSettings.Load(paths));

        Assert.Contains("maxConnections", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ServersGetTheSecuritySettingsAsJson()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings();
        settings.Security.LoginTimeoutSeconds = 77;
        var channel = ServerCatalog.All.Single(server => server.Name == "Kr-Channel");

        var environment = ServerEnvironment.For(channel, settings, paths);

        var passed = GuardSettings.FromJson(environment[GuardSettings.SettingsVariable]);
        Assert.Equal(77, passed.LoginTimeoutSeconds);
        Assert.Equal(settings.Security.ToJson(), passed.ToJson());
    }

    [Fact]
    public void GuardEnvironment_ReadsTheSettingsFileOfTheInstall()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings();
        settings.Security.LoginTimeoutSeconds = 55;
        settings.Save(paths);

        var environment = ServerEnvironment.Guard("Kr-Channel", paths);

        Assert.Equal(55, GuardSettings.FromJson(environment[GuardSettings.SettingsVariable]).LoginTimeoutSeconds);
        Assert.Equal(paths.Security, environment[GuardSettings.StateDirectoryVariable]);
        Assert.Equal("Kr-Channel", environment[GuardSettings.ServerNameVariable]);
    }

    [Fact]
    public void GuardEnvironment_WithoutASettingsFile_UsesTheDefaults()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);

        var environment = ServerEnvironment.Guard("Kr-Lobby", paths);

        Assert.Equal(new GuardSettings().ToJson(), environment[GuardSettings.SettingsVariable]);
        Assert.Equal(paths.Security, environment[GuardSettings.StateDirectoryVariable]);
    }

    [Fact]
    public void GuardEnvironment_IsThePartOfTheServerEnvironment()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings();
        var system = ServerCatalog.All.Single(server => server.Name == "Kr-System");

        var full = ServerEnvironment.For(system, settings, paths);

        foreach (var pair in ServerEnvironment.Guard(system.Name, settings.Security, paths))
        {
            Assert.Equal(pair.Value, full[pair.Key]);
        }
    }
}
