namespace tPoseidon.Core.Common.Tests;

public sealed class ClientFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tposeidon-clientfiles-" + Guid.NewGuid().ToString("N"));

    public ClientFilesTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Data", "Map"));
        File.WriteAllBytes(Path.Combine(_root, "Data", "Map", "Coral_Beach.md3"), new byte[] { 1 });
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void AGameDataPathFindsTheFileWhateverItsCaseAndSlashes()
    {
        var found = ClientFiles.Locate(ClientFiles.Combine(_root, @"data\map\coral_beach.md3"));

        Assert.NotNull(found);
        Assert.True(File.Exists(found));
        Assert.Equal("Coral_Beach.md3", Path.GetFileName(found), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AMissingFileIsNotFound()
    {
        Assert.Null(ClientFiles.Locate(ClientFiles.Combine(_root, "data/map/nowhere.md3")));
        Assert.False(ClientFiles.Exists(ClientFiles.Combine(_root, @"data\other\coral_beach.md3")));
    }

    [Fact]
    public void ADirectoryIsNotReturnedAsAFile()
    {
        Assert.Null(ClientFiles.Locate(ClientFiles.Combine(_root, @"data\map")));
    }
}
