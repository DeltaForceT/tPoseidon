namespace tPoseidon.Launcher.Installer;

public sealed class InstallerException : Exception
{
    public InstallerException(string message)
        : base(message)
    {
    }

    public InstallerException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public interface IInstallFiles
{
    bool FolderExists(string folder);

    void Extract(Stream zip, string folder, IProgress<double>? progress);

    void WriteUninstaller(string target);

    void WriteText(string path, string text);

    void DeleteFolder(string folder);
}

public interface IRunningCheck
{
    bool IsRunning(string executablePath);
}

public interface IShortcutWriter
{
    void Create(ShortcutPlan shortcut);

    void Delete(string path);
}

public interface IUninstallRegistry
{
    void Write(UninstallEntry entry);

    void Delete(string keyPath);
}

public sealed record InstallerServices(IInstallFiles Files, IRunningCheck Running, IShortcutWriter Shortcuts, IUninstallRegistry Registry);

public static class Installer
{
    public static void Install(InstallPlan plan, Stream payload, InstallerServices services, IProgress<double>? progress)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(services);
        RefuseWhenRunning(plan, services, "installed");
        var existed = services.Files.FolderExists(plan.Folder);
        var created = new List<ShortcutPlan>();
        var registered = false;
        try
        {
            services.Files.Extract(payload, plan.Folder, progress);
            services.Files.WriteUninstaller(plan.UninstallerPath);
            services.Files.WriteText(plan.RecordPath, plan.ToJson());
            foreach (var shortcut in plan.Shortcuts)
            {
                services.Shortcuts.Create(shortcut);
                created.Add(shortcut);
            }

            services.Registry.Write(plan.Uninstall);
            registered = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Undo(plan, services, created, registered, deleteFolder: !existed);
            throw new InstallerException($"The installation could not be finished: {ex.Message}", ex);
        }
    }

    public static void Uninstall(InstallPlan plan, InstallerServices services)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(services);
        RefuseWhenRunning(plan, services, "removed");
        try
        {
            foreach (var shortcut in plan.Shortcuts)
            {
                services.Shortcuts.Delete(shortcut.Path);
            }

            services.Registry.Delete(plan.Uninstall.KeyPath);
            services.Files.DeleteFolder(plan.Folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InstallerException($"The launcher could not be removed completely: {ex.Message}", ex);
        }
    }

    private static void RefuseWhenRunning(InstallPlan plan, InstallerServices services, string action)
    {
        if (services.Running.IsRunning(plan.ExecutablePath))
        {
            throw new InstallerException($"{plan.Uninstall.DisplayName} is running. Close it, then try again; it cannot be {action} while it runs.");
        }
    }

    private static void Undo(InstallPlan plan, InstallerServices services, List<ShortcutPlan> created, bool registered, bool deleteFolder)
    {
        foreach (var shortcut in created)
        {
            services.Shortcuts.Delete(shortcut.Path);
        }

        if (registered)
        {
            services.Registry.Delete(plan.Uninstall.KeyPath);
        }

        if (deleteFolder)
        {
            services.Files.DeleteFolder(plan.Folder);
        }
    }
}
