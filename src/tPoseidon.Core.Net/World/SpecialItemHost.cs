using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed record SpecialItemHost
{
    public required Func<bool> InNormalForm { get; init; }
    public required Func<bool> Idle { get; init; }
    public required Func<ushort> MapId { get; init; }
    public required Func<ushort> ReturnMap { get; init; }
    public required Func<ushort, int> MapOption { get; init; }
    public required Func<Encoding> TextEncoding { get; init; }
    public required Func<SpecialPortTables> Ports { get; init; }
    public required Func<IReadOnlyList<short>> StatPoints { get; init; }
    public required Func<IReadOnlyList<short>> GrowthWeights { get; init; }
    public required Func<int> BonusPoints { get; init; }
    public required Func<int> GrowthGroup { get; init; }
    public required Func<int> WarehouseSlots { get; init; }
    public required Func<IReadOnlyDictionary<int, int>> OwnedSkills { get; init; }
    public required Func<int, SkillCardEntry?> CardOfSkill { get; init; }
    public required Func<int, int> MasterTmPoint { get; init; }
    public required Func<byte[]> CharacterInfo { get; init; }
    public required Func<ushort, (ushort X, ushort Y)?, CancellationToken, Task> Warp { get; init; }
    public required Func<byte, CancellationToken, Task<bool>> DyeHair { get; init; }
    public required Func<byte, CancellationToken, Task<bool>> ChangeGender { get; init; }
    public required Func<IReadOnlyList<short>, int, CancellationToken, Task<bool>> ApplyStatPoints { get; init; }
    public required Func<IReadOnlyList<byte>, IReadOnlyList<short>, int, CancellationToken, Task<bool>> ApplyGrowthGraph { get; init; }
    public required Func<int, int, CancellationToken, Task<bool>> ForgetSkill { get; init; }
    public required Func<int, CancellationToken, Task<bool>> ExpandStorage { get; init; }
    public required Func<string, int, CancellationToken, Task<uint>> Rename { get; init; }
    public required Func<string, CancellationToken, Task<uint?>> Shout { get; init; }
}
