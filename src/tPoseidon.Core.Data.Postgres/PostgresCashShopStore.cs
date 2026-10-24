using Npgsql;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresCashShopStore : ICashShopStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCashShopStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public ShopGoods? FindGoods(int goodsCode)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return LoadGoods(conn, null, goodsCode);
    }

    public long Balance(uint accountId, long startingBalance)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return EnsureBalance(conn, null, accountId, startingBalance, forUpdate: false);
    }

    public long Grant(uint accountId, long amount, long startingBalance, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var current = EnsureBalance(conn, tx, accountId, startingBalance, forUpdate: true);
        var updated = Math.Max(0, current + amount);
        SetBalance(conn, tx, accountId, updated);
        using (var log = new NpgsqlCommand("INSERT INTO cash_grant (account_id, amount, reason) VALUES (@a, @n, @r)", conn, tx))
        {
            log.Parameters.AddWithValue("a", (long)accountId);
            log.Parameters.AddWithValue("n", updated - current);
            log.Parameters.AddWithValue("r", reason.Length > 100 ? reason[..100] : reason);
            log.ExecuteNonQuery();
        }

        tx.Commit();
        return updated;
    }

    public IReadOnlyList<ShopFriend> Friends(ulong characterId, uint ownerAccountId)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var cmd = new NpgsqlCommand(
            """
            SELECT c.character_id, c.name, c.account_id
            FROM friend_info f
            JOIN characters owner ON owner.character_id = f.owner_character_id
            JOIN characters c ON c.character_id = f.friend_character_id
            LEFT JOIN deleted_char d ON d.char_uid = c.character_id
            WHERE f.owner_character_id = @ch AND owner.account_id = @a AND NOT f.deleted AND d.char_uid IS NULL
            ORDER BY c.name
            """,
            conn);
        cmd.Parameters.AddWithValue("ch", (long)characterId);
        cmd.Parameters.AddWithValue("a", (long)ownerAccountId);
        using var reader = cmd.ExecuteReader();
        var friends = new List<ShopFriend>();
        while (reader.Read())
        {
            friends.Add(new ShopFriend((ulong)reader.GetInt64(0), reader.GetString(1), (uint)reader.GetInt64(2)));
        }

        return friends;
    }

    public CashShopOutcome Buy(CashPurchase purchase, CashShopPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        ArgumentNullException.ThrowIfNull(policy);
        return Sell(purchase, policy, isGift: false, recipient: null, (conn, tx, goods, option, sellCode) =>
        {
            foreach (var item in goods.Items)
            {
                PostgresChargedItemStore.Insert(conn, tx, new ChargedItemGrant(
                    purchase.AccountId,
                    item.ItemClass,
                    item.ItemIndex,
                    item.ItemCount,
                    option.UseHours,
                    option.LimitFlag,
                    sellCode,
                    (byte)CashSellType.Self));
            }
        });
    }

    public CashShopOutcome Gift(CashGift gift, CashShopPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(gift);
        ArgumentNullException.ThrowIfNull(policy);
        var message = gift.Message.Length > CashShopRules.MaxGiftMessageLength ? gift.Message[..CashShopRules.MaxGiftMessageLength] : gift.Message;
        var recipient = Friends(gift.FromCharacterId, gift.Purchase.AccountId).FirstOrDefault(friend => friend.CharacterId == gift.ToCharacterId);
        return Sell(gift.Purchase, policy, isGift: true, recipient, (conn, tx, goods, option, sellCode) =>
        {
            foreach (var item in goods.Items)
            {
                using var insert = new NpgsqlCommand(
                    """
                    INSERT INTO gift_item (goods_code, goods_category, item_count, item_class, item_index, item_attr, item_use_time, item_flag,
                                           from_account_id, from_char_uid, to_account_id, to_char_uid, gift_date, gift_message, sell_code, limit_code)
                    VALUES (@g, @cat, @cnt, @c, @i, @attr, @t, @f, @fa, @fc, @ta, @tc, NOW(), @m, @s, @l)
                    """,
                    conn,
                    tx);
                insert.Parameters.AddWithValue("g", goods.GoodsCode);
                insert.Parameters.AddWithValue("cat", (short)goods.Category);
                insert.Parameters.AddWithValue("cnt", goods.SetCount);
                insert.Parameters.AddWithValue("c", (short)item.ItemClass);
                insert.Parameters.AddWithValue("i", item.ItemIndex);
                insert.Parameters.AddWithValue("attr", item.ItemCount);
                insert.Parameters.AddWithValue("t", option.UseHours);
                insert.Parameters.AddWithValue("f", (short)option.LimitFlag);
                insert.Parameters.AddWithValue("fa", (long)gift.Purchase.AccountId);
                insert.Parameters.AddWithValue("fc", (long)gift.FromCharacterId);
                insert.Parameters.AddWithValue("ta", (long)recipient!.AccountId);
                insert.Parameters.AddWithValue("tc", (long)recipient.CharacterId);
                insert.Parameters.AddWithValue("m", message);
                insert.Parameters.AddWithValue("s", sellCode);
                insert.Parameters.AddWithValue("l", option.LimitCode);
                insert.ExecuteNonQuery();
            }
        });
    }

    private CashShopOutcome Sell(
        CashPurchase purchase,
        CashShopPolicy policy,
        bool isGift,
        ShopFriend? recipient,
        Action<NpgsqlConnection, NpgsqlTransaction, ShopGoods, ShopGoodsOption, int> deliver)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        var accountExists = AccountExists(conn, tx, purchase.AccountId);
        var balance = policy.Mode == CashMode.Balance && accountExists
            ? EnsureBalance(conn, tx, purchase.AccountId, policy.StartingBalance, forUpdate: true)
            : 0;
        var goods = LoadGoods(conn, tx, purchase.GoodsCode);
        var option = goods?.Options.FirstOrDefault(candidate => candidate.LimitCode == purchase.LimitCode);
        var result = CashShopRules.Check(policy, purchase.World, goods, option, purchase.SettlePrice, balance, accountExists);
        if (result == CashShopResult.Ok && isGift && recipient is null)
        {
            result = CashShopResult.UnknownRecipient;
        }

        if (result != CashShopResult.Ok)
        {
            return new CashShopOutcome(result, 0, balance);
        }

        var price = policy.PriceOf(option!);
        int sellCode;
        using (var sell = new NpgsqlCommand(
            """
            INSERT INTO shop_sell (account_id, goods_code, limit_code, sell_price, sell_world, sell_store, sell_type, to_account_id)
            VALUES (@a, @g, @l, @p, @w, @st, @ty, @to) RETURNING sell_code
            """,
            conn,
            tx))
        {
            sell.Parameters.AddWithValue("a", (long)purchase.AccountId);
            sell.Parameters.AddWithValue("g", purchase.GoodsCode);
            sell.Parameters.AddWithValue("l", purchase.LimitCode);
            sell.Parameters.AddWithValue("p", price);
            sell.Parameters.AddWithValue("w", (short)policy.WorldCode);
            sell.Parameters.AddWithValue("st", (short)purchase.Store);
            sell.Parameters.AddWithValue("ty", (short)(isGift ? CashSellType.Gift : CashSellType.Self));
            sell.Parameters.AddWithValue("to", recipient is null ? DBNull.Value : (long)recipient.AccountId);
            sellCode = (int)sell.ExecuteScalar()!;
        }

        using (var count = new NpgsqlCommand("UPDATE shop_goods SET goods_sellcount = goods_sellcount + 1 WHERE goods_code = @g", conn, tx))
        {
            count.Parameters.AddWithValue("g", purchase.GoodsCode);
            count.ExecuteNonQuery();
        }

        deliver(conn, tx, goods!, option!, sellCode);
        if (policy.Mode == CashMode.Balance)
        {
            balance -= price;
            SetBalance(conn, tx, purchase.AccountId, balance);
        }

        tx.Commit();
        return new CashShopOutcome(CashShopResult.Ok, sellCode, balance);
    }

    private static ShopGoods? LoadGoods(NpgsqlConnection conn, NpgsqlTransaction? tx, int goodsCode)
    {
        string name;
        string description;
        byte category;
        short setCount;
        bool onSale;
        using (var head = new NpgsqlCommand(
            "SELECT goods_name, goods_desc, goods_category, goods_set_count, goods_issell FROM shop_goods WHERE goods_code = @g",
            conn,
            tx))
        {
            head.Parameters.AddWithValue("g", goodsCode);
            using var reader = head.ExecuteReader();
            if (!reader.Read()) return null;
            name = reader.GetString(0);
            description = reader.GetString(1);
            category = (byte)reader.GetInt16(2);
            setCount = reader.GetInt16(3);
            onSale = reader.GetBoolean(4);
        }

        var items = new List<ShopGoodsItem>();
        using (var list = new NpgsqlCommand(
            "SELECT item_class, item_index, item_count FROM shop_goods_item WHERE goods_code = @g ORDER BY item_class, item_index",
            conn,
            tx))
        {
            list.Parameters.AddWithValue("g", goodsCode);
            using var reader = list.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new ShopGoodsItem((byte)reader.GetInt16(0), reader.GetInt32(1), reader.GetInt16(2)));
            }
        }

        var options = new List<ShopGoodsOption>();
        using (var limits = new NpgsqlCommand(
            """
            SELECT g.limit_code, g.goods_limit_price, l.limit_flag, l.limit_use, g.default_display
            FROM shop_goods_limit g JOIN shop_limit l ON l.limit_code = g.limit_code
            WHERE g.goods_code = @g ORDER BY g.default_display DESC, g.limit_code
            """,
            conn,
            tx))
        {
            limits.Parameters.AddWithValue("g", goodsCode);
            using var reader = limits.ExecuteReader();
            while (reader.Read())
            {
                options.Add(new ShopGoodsOption(reader.GetInt16(0), reader.GetInt32(1), (byte)reader.GetInt16(2), reader.GetInt16(3), reader.GetBoolean(4)));
            }
        }

        return new ShopGoods(goodsCode, name, description, category, setCount, onSale, items, options);
    }

    private static bool AccountExists(NpgsqlConnection conn, NpgsqlTransaction tx, uint accountId)
    {
        using var cmd = new NpgsqlCommand("SELECT 1 FROM accounts WHERE account_id = @a", conn, tx);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        return cmd.ExecuteScalar() is not null;
    }

    private static long EnsureBalance(NpgsqlConnection conn, NpgsqlTransaction? tx, uint accountId, long startingBalance, bool forUpdate)
    {
        using (var create = new NpgsqlCommand(
            "INSERT INTO cash_balance (account_id, balance) VALUES (@a, @b) ON CONFLICT (account_id) DO NOTHING",
            conn,
            tx))
        {
            create.Parameters.AddWithValue("a", (long)accountId);
            create.Parameters.AddWithValue("b", Math.Max(0, startingBalance));
            create.ExecuteNonQuery();
        }

        using var read = new NpgsqlCommand(
            "SELECT balance FROM cash_balance WHERE account_id = @a" + (forUpdate ? " FOR UPDATE" : string.Empty),
            conn,
            tx);
        read.Parameters.AddWithValue("a", (long)accountId);
        return (long)read.ExecuteScalar()!;
    }

    private static void SetBalance(NpgsqlConnection conn, NpgsqlTransaction tx, uint accountId, long balance)
    {
        using var cmd = new NpgsqlCommand("UPDATE cash_balance SET balance = @b, updated_at = NOW() WHERE account_id = @a", conn, tx);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("b", balance);
        cmd.ExecuteNonQuery();
    }
}
