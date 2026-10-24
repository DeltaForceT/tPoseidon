using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Core.Net.Tests.InterServer;

public sealed class CharStoreEditWireTests
{
    [Fact]
    public void EveryEditSurvivesTheMasterWire()
    {
        var edits = new[]
        {
            CharacterEdit.Rename("Newname", 19711),
            CharacterEdit.Hair(2, 17),
            CharacterEdit.Gender(Species.Dragon),
            CharacterEdit.GrowthWeights(4, 3, 2, 1),
            CharacterEdit.Storage(88),
        };

        foreach (var edit in edits)
        {
            Assert.Equal(edit, CharStoreWire.DecodeEdit(CharStoreWire.EncodeEdit(edit)));
        }
    }
}
