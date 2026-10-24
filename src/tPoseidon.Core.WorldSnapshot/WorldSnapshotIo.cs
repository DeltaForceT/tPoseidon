using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace tPoseidon.Core.WorldSnapshot;

public static class WorldSnapshotIo
{
    public static void Save(string path, WorldSnapshotDto snapshot)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(snapshot);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var tmp = path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, WorldSnapshotJsonContext.Default.WorldSnapshotDto);
        File.WriteAllBytes(tmp, bytes);
        if (File.Exists(path))
        {
            File.Replace(tmp, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tmp, path);
        }
    }

    public static WorldSnapshotDto? Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        var snapshot = JsonSerializer.Deserialize(bytes, WorldSnapshotJsonContext.Default.WorldSnapshotDto);
        if (snapshot is null) return null;
        if (snapshot.FormatVersion != WorldSnapshotDto.CurrentFormatVersion)
        {
            throw new InvalidDataException(
                $"Snapshot at {path} has FormatVersion={snapshot.FormatVersion}; current is {WorldSnapshotDto.CurrentFormatVersion}. Rebuild via ControlPanel.");
        }
        return snapshot;
    }

        public static string HashFile(string path)
    {
        if (!File.Exists(path)) return string.Empty;
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
