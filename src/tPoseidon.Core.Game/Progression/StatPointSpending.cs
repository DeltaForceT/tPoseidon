namespace tPoseidon.Core.Game.Progression;

public static class StatPointSpending
{
    public const int StatCount = 12;
    public const int RequestSize = StatCount * sizeof(short);

    public static short[] ReadAmounts(ReadOnlySpan<byte> request)
    {
        if (request.Length < RequestSize)
        {
            throw new ArgumentException($"A spending request is {RequestSize} bytes, got {request.Length}.", nameof(request));
        }
        var amounts = new short[StatCount];
        for (var stat = 0; stat < StatCount; stat++)
        {
            amounts[stat] = (short)(request[stat * 2] | (request[stat * 2 + 1] << 8));
        }
        return amounts;
    }

    public static bool TrySpend(short[] statPoints, int bonusPoints, IReadOnlyList<short> amounts, out int bonusPointsLeft)
    {
        ArgumentNullException.ThrowIfNull(statPoints);
        ArgumentNullException.ThrowIfNull(amounts);
        if (statPoints.Length != StatCount || amounts.Count != StatCount)
        {
            throw new ArgumentException($"A character has {StatCount} stats.");
        }

        bonusPointsLeft = bonusPoints;
        var total = 0;
        foreach (var amount in amounts)
        {
            if (amount < 0) return false;
            total += amount;
        }
        if (bonusPoints < total) return false;

        for (var stat = 0; stat < StatCount; stat++)
        {
            statPoints[stat] = unchecked((short)(statPoints[stat] + amounts[stat]));
        }
        bonusPointsLeft = bonusPoints - total;
        return true;
    }
}
