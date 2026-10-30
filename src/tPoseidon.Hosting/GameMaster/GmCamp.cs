using System.Globalization;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public static class GmCamp
{
    public const string GiveName = "camp-give";
    public const string StockName = "camp-stock";

    public static GmOutcome Stock(IMyCampGifts gifts, string character)
    {
        ArgumentNullException.ThrowIfNull(gifts);
        var owner = gifts.Find(character);
        if (owner is null)
        {
            return Unknown(character);
        }

        var stock = gifts.Stock(owner);
        return new GmOutcome(GmOutcome.Done, new[]
        {
            string.Create(CultureInfo.InvariantCulture, $"{owner.CharacterName}: {stock.NewItems} new and {stock.StoredItems} stored My Camp items on the account"),
        });
    }

    public static GmOutcome Give(IMyCampGifts gifts, string character, int itemIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(gifts);
        if (!MyCampGifts.IsItemIndex(itemIndex))
        {
            return Refuse(string.Create(CultureInfo.InvariantCulture, $"{itemIndex} is not a My Camp item id ({MyCampGifts.MinItemIndex} to {MyCampGifts.MaxItemIndex})."));
        }

        if (!MyCampGifts.IsCount(count))
        {
            return Refuse(string.Create(CultureInfo.InvariantCulture, $"{count} is not a count ({MyCampGifts.MinCount} to {MyCampGifts.MaxCount})."));
        }

        var owner = gifts.Find(character);
        if (owner is null)
        {
            return Unknown(character);
        }

        gifts.Give(owner, itemIndex, count);
        return new GmOutcome(GmOutcome.Done, new[]
        {
            string.Create(CultureInfo.InvariantCulture, $"{owner.CharacterName}: added {count} of My Camp item {itemIndex} to the new items of the account; the character sees them in My Camp after the next camp is opened"),
        });
    }

    private static GmOutcome Unknown(string character) => Refuse($"There is no character named '{character}'.");

    private static GmOutcome Refuse(string line) => new(GmOutcome.NotDone, new[] { line }, Refused: true);
}

public sealed class DatabaseGmCampGifts : IMyCampGifts
{
    private readonly DatabaseSettings _database;

    public DatabaseGmCampGifts(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public MyCampOwner? Find(string characterName) => DatabaseAdmin.WithMyCampGifts(_database, gifts => gifts.Find(characterName));

    public MyCampStock Stock(MyCampOwner owner) => DatabaseAdmin.WithMyCampGifts(_database, gifts => gifts.Stock(owner));

    public void Give(MyCampOwner owner, int itemIndex, int count) => DatabaseAdmin.WithMyCampGifts(_database, gifts =>
    {
        gifts.Give(owner, itemIndex, count);
        return 0;
    });
}
