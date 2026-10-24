using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Tests.Dialogs;

public class NpcDialogServiceTests
{
    private static Character MakeChar(ushort mapId = 0) =>
        new()
        {
            CharacterId = 0x10000001,
            OwnerAccountId = 1,
            Slot = 0,
            Name = "TestChar",
            Species = Species.Lion,
            FType = FType.Sense,
            MapId = mapId,
        };

    [Fact]
    public void FreshCharOnCaballa_NoServerDialog()
    {
        var svc = new NpcDialogService();
        var dialogs = svc.GetSpawnDialogs(MakeChar(mapId: NpcDialogService.CoralTownMapId));
        Assert.Empty(dialogs);
    }

    [Fact]
    public void FreshCharOnUnsetMap_NoServerDialog()
    {
        var svc = new NpcDialogService();
        var dialogs = svc.GetSpawnDialogs(MakeChar(mapId: 0));
        Assert.Empty(dialogs);
    }

    [Fact]
    public void FreshCharOnDifferentMap_NoDialog()
    {
        var svc = new NpcDialogService();
        var dialogs = svc.GetSpawnDialogs(MakeChar(mapId: 99));
        Assert.Empty(dialogs);
    }

    [Fact]
    public void NullCharacter_Throws()
    {
        var svc = new NpcDialogService();
        Assert.Throws<ArgumentNullException>(() => svc.GetSpawnDialogs(null!));
    }
}
