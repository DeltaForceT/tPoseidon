using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace tPoseidon.Panel;

public static class PanelAssets
{
    public static Uri Uri(string name) => new($"avares://tPoseidon.Panel/Assets/{name}");

    public static WindowIcon WindowIcon() =>
        new(AssetLoader.Open(Uri(OperatingSystem.IsWindows() ? "tposeidon.ico" : "icon-256.png")));
}

[SupportedOSPlatform("windows")]
internal static class NativeWindows
{
    public const uint HitTestMessage = 0x0084;
    public const int HitClient = 1;
    public const int HitTransparent = -1;
    private const int DwmCloakAttribute = 13;

    public delegate IntPtr SubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    public static bool Cloak(IntPtr window, bool on)
    {
        var value = on ? 1 : 0;
        return DwmSetWindowAttribute(window, DwmCloakAttribute, ref value, sizeof(int)) >= 0;
    }

    public static bool Subclass(IntPtr window, SubclassProc callback) =>
        SetWindowSubclass(window, callback, UIntPtr.Zero, UIntPtr.Zero);

    public static IntPtr DefaultProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam) =>
        DefSubclassProc(window, message, wParam, lParam);

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
