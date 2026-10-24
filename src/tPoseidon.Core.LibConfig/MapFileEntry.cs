namespace tPoseidon.Core.LibConfig;

public sealed record MapFileEntry(int Id, string Name, string FileName)
{
    private const char ClientSeparator = '\\';

    public string BaseName => Path.GetFileNameWithoutExtension(FileName.Replace(ClientSeparator, '/'));
}
