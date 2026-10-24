using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.LibConfig.Tests;

public class JsonNpcPointProviderTests
{
    private const string SyntheticJson = """
        {
          "148": {
            "2":  { "Type": 4, "X": 1062, "Y": 2076 },
            "3":  { "Type": 4, "X": 762,  "Y": 1500 },
            "92": { "Type": 4, "X": 2440, "Y": 1216 }
          },
          "153": {
            "1": { "Type": 1, "X": 3086, "Y": 1303 }
          }
        }
        """;

    private static JsonNpcPointProvider FromSynthetic()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, SyntheticJson);
            return JsonNpcPointProvider.LoadFromFile(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_ParsesAllEntries()
    {
        var p = FromSynthetic();
        Assert.Equal(4, p.Count);
        Assert.Equal(2, p.MapCount);
    }

    [Fact]
    public void TryGet_HeidiOnCoralTown_ReturnsCapturedCoords()
    {
        var p = FromSynthetic();
        Assert.True(p.TryGet(mapId: 148, posId: 2, out var heidi));
        Assert.Equal(1062f, heidi.X);
        Assert.Equal(2076f, heidi.Y);
    }

    [Fact]
    public void TryGet_BunnyMaidOnCoralTown_ReturnsCapturedCoords()
    {
        var p = FromSynthetic();
        Assert.True(p.TryGet(mapId: 148, posId: 3, out var bunny));
        Assert.Equal(762f, bunny.X);
        Assert.Equal(1500f, bunny.Y);
    }

    [Fact]
    public void TryGet_UnknownMapOrPos_ReturnsFalse()
    {
        var p = FromSynthetic();
        Assert.False(p.TryGet(mapId: 999, posId: 2, out _));
        Assert.False(p.TryGet(mapId: 148, posId: 99, out _));
    }

    [Fact]
    public void TryGet_PortalOnFieldMap_ReturnsCapturedCoords()
    {
        var p = FromSynthetic();
        Assert.True(p.TryGet(mapId: 153, posId: 1, out var portal));
        Assert.Equal(3086f, portal.X);
        Assert.Equal(1303f, portal.Y);
    }

    [Fact]
    public void LoadFromFile_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(
            () => JsonNpcPointProvider.LoadFromFile("nonexistent.json"));
    }

    [Fact]
    public void TryGet_DefaultIsZeroOnMiss()
    {
        var p = FromSynthetic();
        var got = p.TryGet(mapId: 999, posId: 99, out var point);
        Assert.False(got);
        Assert.Equal(default(NpcPoint), point);
    }
}
