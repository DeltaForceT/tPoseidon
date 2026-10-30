using tPoseidon.Core.Game.Fiesta;

namespace tPoseidon.Core.Game.Monsters;

public static class ScenarioExit
{
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(15000);

    public static ushort? ZoneOf(ushort storedReturnZone) =>
        storedReturnZone is >= FiestaRules.LowestReturnZone and <= FiestaRules.HighestReturnZone ? storedReturnZone : null;
}
