using System.Globalization;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.GameMaster;

public static class GmCamp
{
    public const string GiveName = "camp-give";
    public const string GiveAllName = "camp-give-all";
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

    public static GmOutcome GiveMatching(IMyCampGifts gifts, GameCatalog catalog, string character, string text)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(text);
        var ids = catalog.FindCampItems(text).Select(item => item.Id).ToArray();
        return GiveAll(gifts, character, ids, catalog.Problem ?? (text.Trim().Length == 0 ? "The game data has no My Camp items." : $"No My Camp item matches '{text.Trim()}'."));
    }

    public static GmOutcome GiveAll(IMyCampGifts gifts, string character, IReadOnlyList<int> itemIndexes, string noneReason)
    {
        ArgumentNullException.ThrowIfNull(gifts);
        ArgumentNullException.ThrowIfNull(itemIndexes);
        var owner = gifts.Find(character);
        if (owner is null)
        {
            return Unknown(character);
        }

        if (itemIndexes.Count == 0)
        {
            return Refuse(noneReason);
        }

        var result = gifts.GiveMany(owner, itemIndexes);
        return new GmOutcome(GmOutcome.Done, new[]
        {
            string.Create(CultureInfo.InvariantCulture, $"{owner.CharacterName}: added {result.Given} My Camp items to the new items of the account, {result.Skipped} skipped; the character sees them in My Camp after the next camp is opened"),
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

    public MyCampBulkGift GiveMany(MyCampOwner owner, IReadOnlyList<int> itemIndexes) => DatabaseAdmin.WithMyCampGifts(_database, gifts => gifts.GiveMany(owner, itemIndexes));

    public void Give(MyCampOwner owner, int itemIndex, int count) => DatabaseAdmin.WithMyCampGifts(_database, gifts =>
    {
        gifts.Give(owner, itemIndex, count);
        return 0;
    });
}
