namespace tPoseidon.Core.LibConfig;

public readonly record struct MapKindEntry(int Id, int Type, int Class, int Option = 0)
{
    public const int TownType = 1;
    public const int SecondReturnType = 2;
    public const int NoReturnClass = 3;
    public const int NoReturnOnArrivalClass = 5;

    public const int NoRebirthItemOption = 0x20;

    public const int NoLoverMoveClass = 2;
    public const int NoLoverMoveOption = 0x100;

    private static readonly int[] NoRebirthItemTypes = { 7, 8, 9 };

    public bool IsReturnMapAtLogin => (Type == TownType || Type == SecondReturnType) && Class != NoReturnClass;

    public bool IsReturnMapOnArrival => IsReturnMapAtLogin && Class != NoReturnOnArrivalClass;

    public bool AllowsLoverMove => Class != NoLoverMoveClass && (Option & NoLoverMoveOption) == 0;

    public bool AllowsRebirthItem =>
        Array.IndexOf(NoRebirthItemTypes, Type) < 0 && (Option & NoRebirthItemOption) == 0;
}
