using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using tPoseidon.LauncherKit;

namespace tPoseidon.Launcher.Installer;

public sealed class DiskInstallFiles : IInstallFiles
{
    private readonly InstallerPayload? _payload;

    public DiskInstallFiles(InstallerPayload? payload)
    {
        _payload = payload;
    }

    public bool FolderExists(string folder) => Directory.Exists(folder);

    public void Extract(Stream zip, string folder, IProgress<double>? progress)
    {
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read);
        var root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        var files = archive.Entries.Where(entry => entry.Name.Length > 0).ToList();
        var total = Math.Max(1, files.Sum(entry => entry.Length));
        var done = 0L;
        foreach (var entry in files)
        {
            var target = Path.GetFullPath(Path.Combine(folder, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The installer holds a file outside its folder: {entry.FullName}.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            done += entry.Length;
            progress?.Report((double)done / total);
        }
    }

    public void WriteUninstaller(string target)
    {
        var payload = _payload ?? throw new InvalidOperationException("There is no installer to copy.");
        payload.CopyStubTo(target);
    }

    public void WriteText(string path, string text) => File.WriteAllText(path, text);

    public void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

public sealed class ProcessRunningCheck : IRunningCheck
{
    public bool IsRunning(string executablePath)
    {
        var full = Path.GetFullPath(executablePath);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(full)))
        {
            using (process)
            {
                if (string.Equals(ImagePath(process), full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ImagePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}

[SupportedOSPlatform("windows")]
public sealed class WindowsShortcutWriter : IShortcutWriter
{
    private const string ShellProgramId = "WScript.Shell";

    public void Create(ShortcutPlan shortcut)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        var type = Type.GetTypeFromProgID(ShellProgramId) ?? throw new InstallerException("Windows could not create shortcuts on this computer.");
        var shell = Activator.CreateInstance(type) ?? throw new InstallerException("Windows could not create shortcuts on this computer.");
        object? link = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcut.Path)!);
            link = type.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [shortcut.Path]);
            var linkType = link!.GetType();
            Set(linkType, link, "TargetPath", shortcut.Target);
            Set(linkType, link, "WorkingDirectory", shortcut.WorkingDirectory);
            Set(linkType, link, "IconLocation", shortcut.IconPath);
            Set(linkType, link, "Description", shortcut.Description);
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
        finally
        {
            if (link is not null)
            {
                Marshal.ReleaseComObject(link);
            }

            Marshal.ReleaseComObject(shell);
        }
    }

    public void Delete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void Set(Type type, object target, string property, string value) =>
        type.InvokeMember(property, BindingFlags.SetProperty, null, target, [value]);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsUninstallRegistry : IUninstallRegistry
{
    public void Write(UninstallEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var key = Registry.CurrentUser.CreateSubKey(entry.KeyPath, writable: true);
        key.SetValue("DisplayName", entry.DisplayName, RegistryValueKind.String);
        key.SetValue("DisplayVersion", entry.DisplayVersion, RegistryValueKind.String);
        key.SetValue("Publisher", entry.Publisher, RegistryValueKind.String);
        key.SetValue("InstallLocation", entry.InstallLocation, RegistryValueKind.String);
        key.SetValue("DisplayIcon", entry.DisplayIcon, RegistryValueKind.String);
        key.SetValue("UninstallString", entry.UninstallString, RegistryValueKind.String);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public void Delete(string keyPath) => Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
}
