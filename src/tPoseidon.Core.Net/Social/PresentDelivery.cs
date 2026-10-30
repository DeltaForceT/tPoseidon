namespace tPoseidon.Core.Net.Social;

public readonly record struct PresentDelivery(bool IsCashBox, ushort Hours)
{
    public static PresentDelivery Bag => default;

    public static PresentDelivery BagFor(ushort hours) => new(false, hours);

    public static PresentDelivery CashBox(ushort hours) => new(true, hours);

    public override string ToString() => IsCashBox ? Hours == 0 ? "cash box" : $"cash box for {Hours} h" : Hours == 0 ? "bag" : $"bag for {Hours} h";
}
