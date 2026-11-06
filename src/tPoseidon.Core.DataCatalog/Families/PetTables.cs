using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Families;

public sealed partial class PetTables
{
    private readonly FastLibConfigReader _reader;
    private readonly Lazy<PetSpeechTables> _petSpeechTables;
    private readonly Lazy<PetRetrainTables> _petRetrainTables;
    private readonly Lazy<CharacterPetReinforceTables> _characterPetReinforceTables;
    private readonly Lazy<PetLevelUpTables> _petLevelUpTables;
    private readonly Lazy<PetDigTables> _petDigTables;
    private readonly Lazy<PetGrowthTables> _petGrowthTables;
    private readonly Lazy<IReadOnlyList<GunParamEntry>> _gunParams;

    public PetTables(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
        _petSpeechTables = new Lazy<PetSpeechTables>(reader.LoadPetSpeechTables);
        _petRetrainTables = new Lazy<PetRetrainTables>(reader.LoadPetRetrainTables);
        _characterPetReinforceTables = new Lazy<CharacterPetReinforceTables>(reader.LoadCharacterPetReinforceTables);
        _petLevelUpTables = new Lazy<PetLevelUpTables>(reader.LoadPetLevelUpTables);
        _petDigTables = new Lazy<PetDigTables>(reader.LoadPetDigTables);
        _petGrowthTables = new Lazy<PetGrowthTables>(reader.LoadPetGrowthTables);
        _gunParams = new Lazy<IReadOnlyList<GunParamEntry>>(reader.LoadGunParams);
    }

    public PetSpeechTables PetSpeechTables => _petSpeechTables.Value;

    public PetRetrainTables PetRetrainTables => _petRetrainTables.Value;

    public CharacterPetReinforceTables CharacterPetReinforceTables => _characterPetReinforceTables.Value;

    public PetLevelUpTables PetLevelUpTables => _petLevelUpTables.Value;

    public PetDigTables PetDigTables => _petDigTables.Value;

    public PetGrowthTables PetGrowthTables => _petGrowthTables.Value;

    public IReadOnlyList<(int ItemId, int MinLevel, int MaxLevel, bool CashPet)> PetComposingPresents(string tableName) => _reader.LoadPetComposingPresents(tableName);

    public IReadOnlyList<GunParamEntry> GunParams => _gunParams.Value;
}
