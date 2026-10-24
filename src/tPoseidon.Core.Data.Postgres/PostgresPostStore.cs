using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class PostgresPostStore : IPostStore
{
    private const byte CommonClass = 1;
    private const byte RareClass = 2;
    private const string ListColumns =
        "l.date_seq, l.post_uid, l.post_type, l.post_sub_type, l.post_state, l.user_type, c.char_name, l.title, l.send_at, l.return_at";

    private readonly NpgsqlDataSource _dataSource;

    public PostgresPostStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public PostResult<PostCounts> Count(ulong charUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        if (OwnerOf(conn, tx, charUid) is not uint accountId) return PostResult.Fail<PostCounts>(101);
        Expire(conn, tx, accountId);
        using var cmd = new NpgsqlCommand($"""
            SELECT COALESCE(SUM(CASE WHEN a.post_state = 0 THEN
                       CASE WHEN a.post_type = 0 AND a.char_uid = @c THEN 1 WHEN a.post_type = 1 THEN 1 ELSE 0 END
                   ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN a.post_state = 2 THEN 1 END), 0)
            FROM ({Latest("d.account_id = @a")}) a
            WHERE a.rn = 1 AND a.post_state IN (0, 2) AND a.return_at > NOW() AND a.user_type = 1
            """, conn, tx);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
        PostCounts counts;
        using (var reader = cmd.ExecuteReader())
        {
            reader.Read();
            counts = new PostCounts((int)reader.GetInt64(0), (int)reader.GetInt64(1));
        }
        tx.Commit();
        return PostResult.Ok<PostCounts>(counts);
    }

    public PostResult<PostSyncResult> Sync(uint accountId, ulong charUid, long afterDateSeq)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var any = new NpgsqlCommand("SELECT 1 FROM post_date WHERE account_id = @a LIMIT 1", conn, tx))
        {
            any.Parameters.AddWithValue("a", (long)accountId);
            if (any.ExecuteScalar() is null)
            {
                tx.Commit();
                return PostResult.Ok<PostSyncResult>(new PostSyncResult(new PostCounts(0, 0), Array.Empty<PostListRow>(), Array.Empty<PostListRow>()));
            }
        }
        Expire(conn, tx, accountId);
        var mine = "d.account_id = @a AND d.char_uid = @c";
        var rows = new List<PostListRow>();
        rows.AddRange(ReadList(conn, tx, ListSql(Latest(mine), counterpart: 0, maxState: 2, postType: PostRules.MailBox, userType: 1, extra: ""), accountId, charUid, 0, 0));
        rows.AddRange(ReadList(conn, tx, ListSql(Latest(mine), counterpart: 1, maxState: 1, postType: PostRules.MailBox, userType: 0, extra: ""), accountId, charUid, 0, 0));
        rows.AddRange(ReadList(conn, tx, ListSql(Latest("d.account_id = @a"), counterpart: 0, maxState: 1, postType: PostRules.GiftBox, userType: 1, extra: ""), accountId, charUid, 0, 0));
        tx.Commit();
        rows.Sort((x, y) => y.DateSeq.CompareTo(x.DateSeq));
        var received = rows.Where(r => r.UserType == PostRules.ReceiverSide).ToList();
        var counts = new PostCounts(
            received.Count(r => r.PostState == PostRules.Unread),
            received.Count(r => r.PostState == PostRules.Returned));
        var mail = rows.Where(r => r.PostType == PostRules.MailBox && (afterDateSeq <= 0 || r.DateSeq > afterDateSeq)).ToList();
        var gifts = rows.Where(r => r.PostType == PostRules.GiftBox && r.UserType == PostRules.ReceiverSide && (afterDateSeq <= 0 || r.DateSeq > afterDateSeq)).ToList();
        return PostResult.Ok<PostSyncResult>(new PostSyncResult(counts, mail, gifts));
    }

    public PostResult<IReadOnlyList<PostListRow>> List(byte listType, uint accountId, ulong charUid, long beforeDateSeq, int rows)
    {
        var mine = "d.account_id = @a AND d.char_uid = @c";
        var page = " AND (@before = 0 OR l.date_seq < @before)";
        var sql = listType switch
        {
            PostRules.ReceivedList => ListSql(Latest(mine), counterpart: 0, maxState: 2, postType: PostRules.MailBox, userType: 1, extra: page),
            PostRules.SentList => ListSql(Latest(mine), counterpart: 1, maxState: 1, postType: PostRules.MailBox, userType: 0, extra: page),
            PostRules.GiftList => ListSql(Latest("d.account_id = @a"), counterpart: 0, maxState: 1, postType: PostRules.GiftBox, userType: 1, extra: page),
            _ => null,
        };
        if (sql is null) return PostResult.Ok<IReadOnlyList<PostListRow>>(Array.Empty<PostListRow>());
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        return PostResult.Ok<IReadOnlyList<PostListRow>>(ReadList(conn, null, sql + " LIMIT @rows", accountId, charUid, beforeDateSeq, rows));
    }

    public PostResult<PostDetail> Detail(uint postUid, uint accountId, ulong charUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        byte postType, subType, state, userType;
        string content;
        int money;
        DateTime sendAt, returnAt;
        using (var cmd = new NpgsqlCommand("""
            SELECT a.post_type, a.post_sub_type, a.post_state, a.content, a.post_money, b.user_type, b.send_at, b.return_at
            FROM post a
            JOIN post_date b ON a.post_uid = b.post_uid AND a.post_state = b.post_state AND b.char_uid <> @c
            WHERE a.post_uid = @p
            ORDER BY b.date_seq DESC
            LIMIT 1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)postUid);
            cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<PostDetail>(101);
            postType = (byte)reader.GetInt16(0);
            subType = (byte)reader.GetInt16(1);
            state = (byte)reader.GetInt16(2);
            content = reader.GetString(3);
            money = reader.GetInt32(4);
            userType = (byte)reader.GetInt16(5);
            sendAt = reader.GetDateTime(6);
            returnAt = reader.GetDateTime(7);
        }
        if (subType == PostRules.Payment && state == PostRules.Returned) money = 0;
        if (postType != PostRules.MailBox) return PostResult.Fail<PostDetail>(102);
        var now = Now(conn, tx);
        if (subType == PostRules.GiftLetter && (sendAt > now || returnAt < now)) return PostResult.Fail<PostDetail>(103);
        if (subType == PostRules.NormalLetter && state is PostRules.Unread or PostRules.Returned && userType == PostRules.SenderSide)
        {
            using (var read = new NpgsqlCommand("UPDATE post SET post_state = 1 WHERE post_uid = @p", conn, tx))
            {
                read.Parameters.AddWithValue("p", (long)postUid);
                if (read.ExecuteNonQuery() != 1) return PostResult.Fail<PostDetail>(103);
            }
            if (CopyStateRows(conn, tx, postUid, fromState: state, toState: PostRules.Read) != 2) return PostResult.Fail<PostDetail>(118);
        }
        var items = new List<PostItemRow>();
        using (var cmd = new NpgsqlCommand("""
            SELECT pi.item_seq, pi.item_uid, pi.item_index, pi.item_class, pi.item_attr, 0, 0, 0, 0
            FROM post_item pi
            WHERE pi.post_uid = @p AND (pi.item_uid = 0 OR pi.item_class = 1)
            UNION ALL
            SELECT pi.item_seq, pi.item_uid, pi.item_index, pi.item_class, pi.item_attr, i.refine_type, i.refine_level, i.refine_state, i.item_slot
            FROM post_item pi
            JOIN items i ON i.item_uid = pi.item_uid
            WHERE pi.post_uid = @p AND pi.item_uid > 0 AND pi.item_class = 2
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)postUid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) items.Add(ReadItem(reader));
        }
        var options = ReadOptions(conn, tx, "SELECT pi.item_seq, pi.item_uid FROM post_item pi WHERE pi.post_uid = @p AND pi.item_uid > 0", postUid);
        tx.Commit();
        return PostResult.Ok<PostDetail>(new PostDetail(postUid, content, money, items, options));
    }

    public PostResult<PostSent> Send(PostSendRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        short keep = request.PostSubType switch
        {
            PostRules.NormalLetter or PostRules.GiftLetter => PostRules.KeepWeek,
            PostRules.PaymentRequest => PostRules.KeepDay,
            _ => -1,
        };
        if (keep < 0) return PostResult.Fail<PostSent>(101);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        long toAccount, toChar;
        using (var cmd = new NpgsqlCommand("SELECT account_id, character_id FROM characters WHERE LOWER(name) = LOWER(@n)", conn, tx))
        {
            cmd.Parameters.AddWithValue("n", request.ToCharName);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<PostSent>(102);
            toAccount = reader.GetInt64(0);
            toChar = reader.GetInt64(1);
            if (reader.Read()) return PostResult.Fail<PostSent>(102);
        }
        if (request.FromAccountId == toAccount || unchecked((long)request.FromCharUid) == toChar) return PostResult.Fail<PostSent>(103);
        if (request.Money < 0 || request.MoneyTax < 0 || request.Money > PostRules.MaxMoney) return PostResult.Fail<PostSent>(104);
        using (var cmd = new NpgsqlCommand("""
            SELECT COUNT(*) FROM post a JOIN post_date b ON a.post_uid = b.post_uid
            WHERE b.account_id = @a AND b.char_uid = @c AND b.user_type = 0
              AND a.created_at >= date_trunc('day', NOW()) AND a.created_at < date_trunc('day', NOW()) + INTERVAL '1 day'
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", (long)request.FromAccountId);
            cmd.Parameters.AddWithValue("c", unchecked((long)request.FromCharUid));
            if ((long)cmd.ExecuteScalar()! >= PostRules.DailySendLimit) return PostResult.Fail<PostSent>(105);
        }
        using (var cmd = new NpgsqlCommand($"""
            SELECT COUNT(*) FROM ({Latest("d.account_id = @a AND d.char_uid = @c AND p.post_type = 0")}) a
            WHERE a.post_state IN (0, 2) AND a.post_sub_type <> 3 AND a.rn = 1 AND a.return_at > NOW() AND a.user_type = 1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", toAccount);
            cmd.Parameters.AddWithValue("c", toChar);
            if ((long)cmd.ExecuteScalar()! >= PostRules.InboxLimit) return PostResult.Fail<PostSent>(106);
        }
        long postUid;
        DateTime occur, returnAt;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO post (post_type, post_sub_type, post_state, title, content, post_money_tax, post_money, post_money_take_at, sell_code, parent_post_uid, created_at)
            VALUES (0, @st, 0, @t, @ct, @tax, @m, NULL, 0, 0, NOW())
            RETURNING post_uid, created_at, post_keep_until(created_at, @keep)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("st", (short)request.PostSubType);
            cmd.Parameters.AddWithValue("t", request.Title);
            cmd.Parameters.AddWithValue("ct", request.Content);
            cmd.Parameters.AddWithValue("tax", request.MoneyTax);
            cmd.Parameters.AddWithValue("m", request.Money);
            cmd.Parameters.AddWithValue("keep", keep);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            postUid = reader.GetInt64(0);
            occur = reader.GetDateTime(1);
            returnAt = reader.GetDateTime(2);
        }
        var dateSeq = InsertDate(conn, tx, postUid, PostRules.Unread, PostRules.SenderSide, request.FromAccountId, unchecked((long)request.FromCharUid), request.FromCharName, request.FromCharLevel, occur, returnAt);
        InsertDate(conn, tx, postUid, PostRules.Unread, PostRules.ReceiverSide, (uint)toAccount, toChar, request.ToCharName, request.FromCharLevel, occur, returnAt);
        foreach (var attached in request.Items)
        {
            if (attached.Count == 0) continue;
            if (!Attach(conn, tx, postUid, unchecked((long)request.FromCharUid), attached)) return PostResult.Fail<PostSent>(110);
        }
        tx.Commit();
        return PostResult.Ok<PostSent>(new PostSent(dateSeq, (uint)postUid, occur, returnAt, (uint)toAccount, unchecked((ulong)toChar)));
    }

    public PostResult<PostReceived> Receive(PostReceiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        Expire(conn, tx, request.ToAccountId);
        byte postType, subType, state;
        int money, tax;
        bool moneyTaken;
        long fromAccount, fromChar;
        string fromName;
        int fromLevel;
        DateTime sendAt, returnAt;
        using (var cmd = new NpgsqlCommand("""
            SELECT a.post_type, a.post_sub_type, a.post_state, a.post_money, a.post_money_tax, a.post_money_take_at IS NOT NULL,
                   b.account_id, b.char_uid, b.char_name, c.level, b.send_at, b.return_at
            FROM post a
            JOIN post_date b ON a.post_uid = b.post_uid AND a.post_state = b.post_state
            JOIN characters c ON c.character_id = b.char_uid
            WHERE a.post_uid = @p AND b.user_type = 0
            ORDER BY b.date_seq DESC
            LIMIT 1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)request.PostUid);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<PostReceived>(101);
            postType = (byte)reader.GetInt16(0);
            subType = (byte)reader.GetInt16(1);
            state = (byte)reader.GetInt16(2);
            money = reader.GetInt32(3);
            tax = reader.GetInt32(4);
            moneyTaken = reader.GetBoolean(5);
            fromAccount = reader.GetInt64(6);
            fromChar = reader.GetInt64(7);
            fromName = reader.GetString(8);
            fromLevel = reader.GetInt32(9);
            sendAt = reader.GetDateTime(10);
            returnAt = reader.GetDateTime(11);
        }
        if (state != request.PostState) return PostResult.Fail<PostReceived>(102);
        var now = Now(conn, tx);
        if (postType == PostRules.MailBox && subType == PostRules.GiftLetter && (sendAt > now || returnAt < now)) return PostResult.Fail<PostReceived>(103);
        long moneyChange = 0;
        uint? paymentAccount = null;
        ulong? paymentChar = null;
        if (subType == PostRules.PaymentRequest && money > 0 && !moneyTaken)
        {
            if (state == PostRules.Unread)
            {
                moneyChange = -money;
                long paymentUid;
                DateTime paymentAt, paymentReturn;
                using (var cmd = new NpgsqlCommand("""
                    INSERT INTO post (post_type, post_sub_type, post_state, title, content, post_money_tax, post_money, post_money_take_at, sell_code, parent_post_uid, created_at)
                    VALUES (0, 3, 0, @t, @ct, 0, @m, NULL, 0, @parent, NOW())
                    RETURNING post_uid, created_at, post_keep_until(created_at, 1)
                    """, conn, tx))
                {
                    cmd.Parameters.AddWithValue("t", request.SysTitle);
                    cmd.Parameters.AddWithValue("ct", request.SysContent);
                    cmd.Parameters.AddWithValue("m", money - tax);
                    cmd.Parameters.AddWithValue("parent", (long)request.PostUid);
                    using var reader = cmd.ExecuteReader();
                    reader.Read();
                    paymentUid = reader.GetInt64(0);
                    paymentAt = reader.GetDateTime(1);
                    paymentReturn = reader.GetDateTime(2);
                }
                InsertDate(conn, tx, paymentUid, PostRules.Unread, PostRules.SenderSide, request.ToAccountId, unchecked((long)request.ToCharUid), request.ToCharName, request.ToCharLevel, paymentAt, paymentReturn);
                InsertDate(conn, tx, paymentUid, PostRules.Unread, PostRules.ReceiverSide, (uint)fromAccount, fromChar, fromName, fromLevel, paymentAt, paymentReturn);
                paymentAccount = (uint)fromAccount;
                paymentChar = unchecked((ulong)fromChar);
            }
        }
        else if (subType is PostRules.GiftLetter or PostRules.Payment && money > 0 && !moneyTaken)
        {
            moneyChange = money;
        }
        var received = new List<PostReceivedItem>();
        if (!TakeItems(conn, tx, request, received, out var itemError)) return PostResult.Fail<PostReceived>(itemError);
        var setMoney = money > 0 ? ", post_money_take_at = NOW()" : "";
        var guard = money > 0 ? " AND post_money_take_at IS NULL" : "";
        using (var cmd = new NpgsqlCommand($"UPDATE post SET post_state = 1{setMoney} WHERE post_uid = @p{guard}", conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)request.PostUid);
            if (cmd.ExecuteNonQuery() != 1) return PostResult.Fail<PostReceived>(money > 0 ? 117 : 116);
        }
        if (CopyStateRows(conn, tx, request.PostUid, fromState: request.PostState, toState: PostRules.Read) != 2) return PostResult.Fail<PostReceived>(118);
        IReadOnlyList<PostItemOption> options = received.Count == 0
            ? Array.Empty<PostItemOption>()
            : ReadOptions(conn, tx, "SELECT pi.item_seq, pi.item_uid FROM post_item pi WHERE pi.post_uid = @p AND pi.item_class = 2 AND pi.item_uid > 0", request.PostUid);
        tx.Commit();
        return PostResult.Ok<PostReceived>(new PostReceived(subType, moneyChange, received, options, paymentAccount, paymentChar));
    }

    public PostResult<PostListRow?> Delete(uint postUid, uint accountId, ulong charUid, long lastDateSeq)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        Expire(conn, tx, accountId);
        byte postType, subType, state, userType;
        long dateSeq;
        using (var cmd = new NpgsqlCommand($"""
            SELECT a.date_seq, a.post_type, a.post_sub_type, a.post_state, a.user_type
            FROM ({Latest("d.account_id = @a AND d.post_uid = @p AND d.char_uid = CASE WHEN p.post_type = 0 THEN @c ELSE d.char_uid END")}) a
            WHERE a.rn = 1 AND a.return_at > NOW()
            ORDER BY a.date_seq DESC
            LIMIT 1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", (long)accountId);
            cmd.Parameters.AddWithValue("p", (long)postUid);
            cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<PostListRow?>(101);
            dateSeq = reader.GetInt64(0);
            postType = (byte)reader.GetInt16(1);
            subType = (byte)reader.GetInt16(2);
            state = (byte)reader.GetInt16(3);
            userType = (byte)reader.GetInt16(4);
        }
        var deleteMode = DeleteMode(postType, subType, state, userType);
        if (deleteMode == DeleteNotAllowed) return PostResult.Fail<PostListRow?>(101);
        if (deleteMode == DeleteRemove)
        {
            using (var cmd = new NpgsqlCommand("""
                INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
                SELECT post_uid, 3, user_type, account_id, char_uid, char_name, char_level, NOW(), post_keep_until(NOW(), 0), NOW()
                FROM post_date WHERE date_seq = @d AND post_uid = @p
                """, conn, tx))
            {
                cmd.Parameters.AddWithValue("d", dateSeq);
                cmd.Parameters.AddWithValue("p", (long)postUid);
                cmd.ExecuteNonQuery();
            }
            var close = postType == PostRules.MailBox
                ? "UPDATE post SET post_state = 3 WHERE post_uid = @p AND (SELECT COUNT(*) FROM post_date WHERE post_uid = @p AND post_state = 3) = 2"
                : "UPDATE post SET post_state = 3 WHERE post_uid = @p";
            using var closeCmd = new NpgsqlCommand(close, conn, tx);
            closeCmd.Parameters.AddWithValue("p", (long)postUid);
            closeCmd.ExecuteNonQuery();
        }
        else
        {
            using (var cmd = new NpgsqlCommand("""
                INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
                SELECT post_uid, 2, CASE WHEN user_type = 0 THEN 1 WHEN user_type = 1 THEN 0 END,
                       account_id, char_uid, char_name, char_level, NOW(), post_keep_until(NOW(), 1), NOW()
                FROM post_date WHERE post_uid = @p AND post_state = 0
                ORDER BY date_seq
                """, conn, tx))
            {
                cmd.Parameters.AddWithValue("p", (long)postUid);
                cmd.ExecuteNonQuery();
            }
            using var returned = new NpgsqlCommand("UPDATE post SET post_state = 2 WHERE post_uid = @p", conn, tx);
            returned.Parameters.AddWithValue("p", (long)postUid);
            returned.ExecuteNonQuery();
        }
        var filter = postType == PostRules.MailBox
            ? "d.account_id = @a AND d.char_uid = @c AND d.post_uid < @p AND d.date_seq < @last"
            : "d.account_id = @a AND d.post_uid < @p AND d.date_seq < @last";
        PostListRow? next;
        using (var cmd = new NpgsqlCommand($"""
            SELECT {ListColumns}
            FROM (SELECT * FROM ({Latest(filter)}) x
                  WHERE x.rn = 1 AND x.post_state <= 2 AND x.return_at > NOW() AND x.user_type = 1
                  ORDER BY x.post_uid DESC LIMIT 1) l
            JOIN LATERAL ({Counterpart(0)}) c ON TRUE
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", (long)accountId);
            cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
            cmd.Parameters.AddWithValue("p", (long)postUid);
            cmd.Parameters.AddWithValue("last", lastDateSeq);
            using var reader = cmd.ExecuteReader();
            next = reader.Read() ? ReadListRow(reader) : null;
        }
        tx.Commit();
        return PostResult.Ok<PostListRow?>(next);
    }

    public PostResult<PostSent> SendGmGift(GmGift gift)
    {
        ArgumentNullException.ThrowIfNull(gift);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        long toAccount;
        string toName;
        int toLevel;
        using (var cmd = new NpgsqlCommand("SELECT account_id, name, level FROM characters WHERE character_id = @c", conn, tx))
        {
            cmd.Parameters.AddWithValue("c", unchecked((long)gift.ToCharUid));
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<PostSent>(102);
            toAccount = reader.GetInt64(0);
            toName = reader.GetString(1);
            toLevel = reader.GetInt32(2);
        }
        long postUid;
        DateTime occur, returnAt;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO post (post_type, post_sub_type, post_state, title, content, post_money_tax, post_money, post_money_take_at, sell_code, parent_post_uid, created_at)
            VALUES (1, 5, 0, @t, @ct, 0, 0, NULL, 0, 0, NOW())
            RETURNING post_uid, created_at, post_keep_until(created_at, 0)
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("t", gift.Title);
            cmd.Parameters.AddWithValue("ct", gift.Content);
            using var reader = cmd.ExecuteReader();
            reader.Read();
            postUid = reader.GetInt64(0);
            occur = reader.GetDateTime(1);
            returnAt = reader.GetDateTime(2);
        }
        InsertDate(conn, tx, postUid, PostRules.Unread, PostRules.SenderSide, 0, 0, gift.GmName, 1, occur, returnAt);
        var dateSeq = InsertDate(conn, tx, postUid, PostRules.Unread, PostRules.ReceiverSide, (uint)toAccount, unchecked((long)gift.ToCharUid), toName, toLevel, occur, returnAt);
        foreach (var item in gift.Items)
        {
            var timed = item.ItemClass == RareClass && item.AddMinute > 0;
            byte flag = item.ItemClass == RareClass && item.ItemAttr == 1 && item.AddMinute == 0 ? (byte)0
                : timed ? (byte)GiftRules.TimedFlag
                : item.ItemClass == CommonClass ? (byte)2
                : item.ItemClass == RareClass && item.ItemAttr > 1 ? (byte)2
                : (byte)0;
            using var cmd = new NpgsqlCommand("""
                INSERT INTO post_item (post_uid, item_uid, item_class, item_index, item_attr, add_minute, item_flag, created_at)
                VALUES (@p, 0, @cl, @i, @n, @m, @f, NOW())
                """, conn, tx);
            cmd.Parameters.AddWithValue("p", postUid);
            cmd.Parameters.AddWithValue("cl", (short)item.ItemClass);
            cmd.Parameters.AddWithValue("i", item.ItemIndex);
            cmd.Parameters.AddWithValue("n", item.ItemAttr);
            cmd.Parameters.AddWithValue("m", timed ? item.AddMinute : 0);
            cmd.Parameters.AddWithValue("f", (short)flag);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
        return PostResult.Ok(new PostSent(dateSeq, (uint)postUid, occur, returnAt, (uint)toAccount, gift.ToCharUid));
    }

    public PostResult<GiftDetail> GiftDetail(uint postUid, uint accountId, ulong charUid)
    {
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        byte postType, state;
        string content;
        using (var cmd = new NpgsqlCommand("""
            SELECT a.post_type, a.post_state, a.content
            FROM post a
            JOIN post_date b ON a.post_uid = b.post_uid AND a.post_state = b.post_state AND b.account_id <> @a
            WHERE a.post_uid = @p
            ORDER BY b.date_seq DESC
            LIMIT 1
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)postUid);
            cmd.Parameters.AddWithValue("a", (long)accountId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return PostResult.Fail<GiftDetail>(101);
            postType = (byte)reader.GetInt16(0);
            state = (byte)reader.GetInt16(1);
            content = reader.GetString(2);
        }
        if (postType != PostRules.GiftBox) return PostResult.Fail<GiftDetail>(102);
        var items = new List<GiftDetailItem>();
        using (var cmd = new NpgsqlCommand(
            "SELECT item_seq, item_index, item_class, item_attr, add_minute, item_flag FROM post_item WHERE post_uid = @p ORDER BY item_seq", conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)postUid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                items.Add(new GiftDetailItem(reader.GetInt64(0), reader.GetInt32(1), (byte)reader.GetInt16(2), reader.GetInt16(3), reader.GetInt32(4), (byte)reader.GetInt16(5)));
            }
        }
        if (items.Count == 0 && state == PostRules.Unread && !MarkGiftRead(conn, tx, postUid, state, charUid))
        {
            return PostResult.Fail<GiftDetail>(101);
        }
        tx.Commit();
        return PostResult.Ok(new GiftDetail(postUid, content, items));
    }

    public PostResult<IReadOnlyList<GiftReceivedItem>> ReceiveGift(GiftReceiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var conn = _dataSource.OpenConnectionForActiveSchema();
        using var tx = conn.BeginTransaction();
        using (var cmd = new NpgsqlCommand($"""
            SELECT 1 FROM ({Latest("d.account_id = @a AND p.post_uid = @p")}) l
            JOIN LATERAL ({Counterpart(0)}) c ON TRUE
            WHERE l.rn = 1 AND l.post_state = 0 AND l.user_type = 1 AND l.return_at > NOW()
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", (long)request.ToAccountId);
            cmd.Parameters.AddWithValue("p", (long)request.PostUid);
            if (cmd.ExecuteScalar() is null) return PostResult.Fail<IReadOnlyList<GiftReceivedItem>>(101);
        }
        var pending = new List<(long Seq, int Index, byte Class, short Attr, int AddMinute)>();
        using (var cmd = new NpgsqlCommand(
            "SELECT item_seq, item_index, item_class, item_attr, add_minute FROM post_item WHERE post_uid = @p AND take_at IS NULL ORDER BY item_seq", conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)request.PostUid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) pending.Add((reader.GetInt64(0), reader.GetInt32(1), (byte)reader.GetInt16(2), reader.GetInt16(3), reader.GetInt32(4)));
        }
        var toChar = unchecked((long)request.ToCharUid);
        var used = InventoryPositions(conn, tx, toChar);
        var received = new List<GiftReceivedItem>(pending.Count);
        foreach (var item in pending)
        {
            long created;
            DateTime breaks;
            using (var insert = new NpgsqlCommand("""
                INSERT INTO items (item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
                VALUES (@i, @cl, 0, 0,
                        CASE WHEN @cl = 2 AND @m > 0 THEN NOW() + make_interval(mins => @m) ELSE TIMESTAMPTZ '2020-01-01 00:00:00+00' END,
                        0, 0, 0, 0, @n)
                RETURNING item_uid, item_breaked
                """, conn, tx))
            {
                insert.Parameters.AddWithValue("i", item.Index);
                insert.Parameters.AddWithValue("cl", (short)item.Class);
                insert.Parameters.AddWithValue("m", item.AddMinute);
                insert.Parameters.AddWithValue("n", (int)Math.Max((short)0, item.Attr));
                using var reader = insert.ExecuteReader();
                reader.Read();
                created = reader.GetInt64(0);
                breaks = reader.GetDateTime(1);
            }
            using (var own = new NpgsqlCommand("INSERT INTO item_char (char_uid, item_uid, item_where, item_pos) VALUES (@c, @u, @w, @pos)", conn, tx))
            {
                own.Parameters.AddWithValue("c", toChar);
                own.Parameters.AddWithValue("u", created);
                own.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
                own.Parameters.AddWithValue("pos", NextFree(used));
                if (own.ExecuteNonQuery() != 1) return PostResult.Fail<IReadOnlyList<GiftReceivedItem>>(103);
            }
            if (item.Class == RareClass)
            {
                short position = 0;
                foreach (var option in request.Options.Where(o => o.ItemSeq == item.Seq))
                {
                    using var opt = new NpgsqlCommand("""
                        INSERT INTO item_option (item_uid, position, prop_type, prop_id, prop_value, prop_extra)
                        VALUES (@u, @pos, @t, @id, @v, @x)
                        """, conn, tx);
                    opt.Parameters.AddWithValue("u", created);
                    opt.Parameters.AddWithValue("pos", position++);
                    opt.Parameters.AddWithValue("t", (short)GiftRules.StatOptionType);
                    opt.Parameters.AddWithValue("id", (int)option.OptionIndex);
                    opt.Parameters.AddWithValue("v", (short)Math.Clamp(option.OptionValue, short.MinValue, short.MaxValue));
                    opt.Parameters.AddWithValue("x", (long)option.MixedItem);
                    opt.ExecuteNonQuery();
                }
            }
            using (var taken = new NpgsqlCommand(
                "UPDATE post_item SET take_char_uid = @c, take_char_level = @l, take_at = NOW(), item_uid = @u WHERE item_seq = @s", conn, tx))
            {
                taken.Parameters.AddWithValue("c", toChar);
                taken.Parameters.AddWithValue("l", (short)Math.Clamp(request.ToCharLevel, short.MinValue, short.MaxValue));
                taken.Parameters.AddWithValue("u", created);
                taken.Parameters.AddWithValue("s", item.Seq);
                if (taken.ExecuteNonQuery() != 1) return PostResult.Fail<IReadOnlyList<GiftReceivedItem>>(103);
            }
            received.Add(new GiftReceivedItem(item.Seq, unchecked((ulong)created), item.Index, item.Class, (ushort)Math.Max((short)0, item.Attr), breaks));
        }
        if (!MarkGiftRead(conn, tx, request.PostUid, PostRules.Unread, request.ToCharUid))
        {
            return PostResult.Fail<IReadOnlyList<GiftReceivedItem>>(101);
        }
        tx.Commit();
        return PostResult.Ok<IReadOnlyList<GiftReceivedItem>>(received);
    }

    private static bool MarkGiftRead(NpgsqlConnection conn, NpgsqlTransaction tx, uint postUid, byte fromState, ulong receiverChar)
    {
        using (var read = new NpgsqlCommand("UPDATE post SET post_state = 1 WHERE post_uid = @p", conn, tx))
        {
            read.Parameters.AddWithValue("p", (long)postUid);
            if (read.ExecuteNonQuery() != 1) return false;
        }
        using var copy = new NpgsqlCommand("""
            INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
            SELECT post_uid, 1, user_type, account_id, CASE WHEN user_type = 0 THEN char_uid ELSE @c END, char_name, char_level, NOW(),
                   CASE WHEN user_type = 0 THEN return_at ELSE post_keep_until(NOW(), 1) END, NOW()
            FROM post_date WHERE post_uid = @p AND post_state = @from
            ORDER BY date_seq
            """, conn, tx);
        copy.Parameters.AddWithValue("p", (long)postUid);
        copy.Parameters.AddWithValue("from", (short)fromState);
        copy.Parameters.AddWithValue("c", unchecked((long)receiverChar));
        return copy.ExecuteNonQuery() == 2;
    }

    private static HashSet<short> InventoryPositions(NpgsqlConnection conn, NpgsqlTransaction tx, long charUid)
    {
        var used = new HashSet<short>();
        using var cmd = new NpgsqlCommand("SELECT item_pos FROM item_char WHERE char_uid = @c AND item_where = @w", conn, tx);
        cmd.Parameters.AddWithValue("c", charUid);
        cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) used.Add(reader.GetInt16(0));
        return used;
    }

    private static short NextFree(HashSet<short> used)
    {
        short pos = 0;
        while (used.Contains(pos)) pos++;
        used.Add(pos);
        return pos;
    }

    private const int DeleteNotAllowed = 0;
    private const int DeleteRemove = 1;
    private const int DeleteReturn = 2;

    private static int DeleteMode(byte postType, byte subType, byte state, byte userType)
    {
        if (userType == PostRules.SenderSide) return DeleteRemove;
        return state switch
        {
            PostRules.Unread => postType == PostRules.MailBox && subType is PostRules.NormalLetter or PostRules.GiftLetter or PostRules.PaymentRequest
                ? DeleteReturn
                : DeleteNotAllowed,
            PostRules.Read => DeleteRemove,
            _ => DeleteNotAllowed,
        };
    }

    private static string Latest(string filter) => $"""
        SELECT p.post_type, p.post_sub_type, p.title, d.date_seq, d.post_uid, d.post_state, d.user_type,
               d.account_id, d.char_uid, d.char_name, d.send_at, d.return_at,
               RANK() OVER (PARTITION BY d.post_uid ORDER BY d.date_seq DESC) AS rn
        FROM post p
        JOIN post_date d ON p.post_uid = d.post_uid AND p.post_state < 3
        WHERE {filter}
        """;

    private static string ListSql(string latest, int counterpart, int maxState, byte postType, int userType, string extra) => $"""
        SELECT {ListColumns}
        FROM ({latest}) l
        JOIN LATERAL ({Counterpart(counterpart)}) c ON TRUE
        WHERE l.rn = 1 AND l.post_state <= {maxState} AND l.return_at > NOW() AND l.post_type = {postType} AND l.user_type = {userType}{extra}
        ORDER BY l.date_seq DESC
        """;

    private static string Counterpart(int userType) => $"""
        SELECT c.char_name FROM post_date c
        WHERE c.post_uid = l.post_uid AND c.post_state = l.post_state AND c.user_type = {userType}
        ORDER BY c.date_seq DESC LIMIT 1
        """;

    private static List<PostListRow> ReadList(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, uint accountId, ulong charUid, long before, int rows)
    {
        using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
        cmd.Parameters.AddWithValue("before", before);
        cmd.Parameters.AddWithValue("rows", rows);
        using var reader = cmd.ExecuteReader();
        var result = new List<PostListRow>();
        while (reader.Read()) result.Add(ReadListRow(reader));
        return result;
    }

    private static PostListRow ReadListRow(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        (uint)reader.GetInt64(1),
        (byte)reader.GetInt16(2),
        (byte)reader.GetInt16(3),
        (byte)reader.GetInt16(4),
        (byte)reader.GetInt16(5),
        reader.GetString(6),
        reader.GetString(7),
        reader.GetDateTime(8),
        reader.GetDateTime(9));

    private static PostItemRow ReadItem(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        unchecked((ulong)reader.GetInt64(1)),
        reader.GetInt32(2),
        (byte)reader.GetInt16(3),
        reader.GetInt16(4),
        (byte)Convert.ToInt16(reader.GetValue(5)),
        (byte)Convert.ToInt16(reader.GetValue(6)),
        (byte)Convert.ToInt16(reader.GetValue(7)),
        (byte)Convert.ToInt16(reader.GetValue(8)));

    private static List<PostItemOption> ReadOptions(NpgsqlConnection conn, NpgsqlTransaction tx, string itemsSql, uint postUid)
    {
        using var cmd = new NpgsqlCommand($"""
            SELECT a.item_seq, o.item_uid, o.prop_type, o.prop_id, o.prop_value, o.prop_extra
            FROM ({itemsSql}) a
            JOIN item_option o ON o.item_uid = a.item_uid
            ORDER BY a.item_seq, o.position
            """, conn, tx);
        cmd.Parameters.AddWithValue("p", (long)postUid);
        using var reader = cmd.ExecuteReader();
        var result = new List<PostItemOption>();
        while (reader.Read())
        {
            result.Add(new PostItemOption(
                reader.GetInt64(0),
                unchecked((ulong)reader.GetInt64(1)),
                (byte)reader.GetInt16(2),
                (ushort)reader.GetInt32(3),
                reader.GetInt16(4),
                unchecked((uint)reader.GetInt64(5))));
        }
        return result;
    }

    private static uint? OwnerOf(NpgsqlConnection conn, NpgsqlTransaction tx, ulong charUid)
    {
        using var cmd = new NpgsqlCommand("SELECT account_id FROM characters WHERE character_id = @c", conn, tx);
        cmd.Parameters.AddWithValue("c", unchecked((long)charUid));
        return cmd.ExecuteScalar() is long account ? (uint)account : null;
    }

    private static DateTime Now(NpgsqlConnection conn, NpgsqlTransaction tx)
    {
        using var cmd = new NpgsqlCommand("SELECT NOW()", conn, tx);
        return (DateTime)cmd.ExecuteScalar()!;
    }

    private static long InsertDate(NpgsqlConnection conn, NpgsqlTransaction tx, long postUid, byte state, byte userType,
        uint accountId, long charUid, string charName, int charLevel, DateTime sendAt, DateTime returnAt)
    {
        using var cmd = new NpgsqlCommand("""
            INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
            VALUES (@p, @s, @u, @a, @c, @n, @l, @send, @ret, NOW())
            RETURNING date_seq
            """, conn, tx);
        cmd.Parameters.AddWithValue("p", postUid);
        cmd.Parameters.AddWithValue("s", (short)state);
        cmd.Parameters.AddWithValue("u", (short)userType);
        cmd.Parameters.AddWithValue("a", (long)accountId);
        cmd.Parameters.AddWithValue("c", charUid);
        cmd.Parameters.AddWithValue("n", charName);
        cmd.Parameters.AddWithValue("l", (short)Math.Clamp(charLevel, short.MinValue, short.MaxValue));
        cmd.Parameters.AddWithValue("send", sendAt);
        cmd.Parameters.AddWithValue("ret", returnAt);
        return (long)cmd.ExecuteScalar()!;
    }

    private static int CopyStateRows(NpgsqlConnection conn, NpgsqlTransaction tx, uint postUid, byte fromState, byte toState)
    {
        using var cmd = new NpgsqlCommand("""
            INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
            SELECT post_uid, @to, user_type, account_id, char_uid, char_name, char_level, NOW(),
                   CASE WHEN user_type = 0 THEN return_at ELSE post_keep_until(NOW(), 1) END, NOW()
            FROM post_date WHERE post_uid = @p AND post_state = @from
            ORDER BY date_seq
            """, conn, tx);
        cmd.Parameters.AddWithValue("p", (long)postUid);
        cmd.Parameters.AddWithValue("from", (short)fromState);
        cmd.Parameters.AddWithValue("to", (short)toState);
        return cmd.ExecuteNonQuery();
    }

    private static bool Attach(NpgsqlConnection conn, NpgsqlTransaction tx, long postUid, long fromChar, PostSendItem attached)
    {
        int index;
        byte itemClass;
        int stack;
        using (var cmd = new NpgsqlCommand("""
            SELECT i.item_index, i.item_class, i.item_stack
            FROM items i JOIN item_char c ON c.item_uid = i.item_uid
            WHERE i.item_uid = @u AND c.char_uid = @c AND c.item_where = @w
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("u", unchecked((long)attached.ItemUid));
            cmd.Parameters.AddWithValue("c", fromChar);
            cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;
            index = reader.GetInt32(0);
            itemClass = (byte)reader.GetInt16(1);
            stack = reader.GetInt32(2);
        }
        if (attached.Count > stack) return false;
        var partial = itemClass == CommonClass && attached.Count < stack;
        var sql = partial
            ? "UPDATE items SET item_stack = item_stack - @n WHERE item_uid = @u"
            : itemClass == CommonClass
                ? "DELETE FROM items WHERE item_uid = @u"
                : "DELETE FROM item_char WHERE item_uid = @u";
        using (var cmd = new NpgsqlCommand(sql, conn, tx))
        {
            cmd.Parameters.AddWithValue("u", unchecked((long)attached.ItemUid));
            cmd.Parameters.AddWithValue("n", (int)attached.Count);
            if (cmd.ExecuteNonQuery() != 1) return false;
        }
        byte flag = itemClass == RareClass && attached.Count == 1 ? (byte)0 : itemClass is CommonClass or RareClass ? (byte)2 : (byte)0;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO post_item (post_uid, item_uid, item_class, item_index, item_attr, add_minute, item_flag, created_at)
            VALUES (@p, @u, @cl, @i, @n, 0, @f, NOW())
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", postUid);
            cmd.Parameters.AddWithValue("u", unchecked((long)attached.ItemUid));
            cmd.Parameters.AddWithValue("cl", (short)itemClass);
            cmd.Parameters.AddWithValue("i", index);
            cmd.Parameters.AddWithValue("n", (short)attached.Count);
            cmd.Parameters.AddWithValue("f", (short)flag);
            cmd.ExecuteNonQuery();
        }
        return true;
    }

    private static bool TakeItems(NpgsqlConnection conn, NpgsqlTransaction tx, PostReceiveRequest request, List<PostReceivedItem> received, out int error)
    {
        error = PostRules.Ok;
        var pending = new List<(long Seq, long Uid, byte Class, int Index, short Attr, short Slot)>();
        using (var cmd = new NpgsqlCommand("""
            SELECT item_seq, item_uid, item_class, item_index, item_attr
            FROM post_item WHERE post_uid = @p AND take_at IS NULL ORDER BY item_seq
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("p", (long)request.PostUid);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) pending.Add((reader.GetInt64(0), reader.GetInt64(1), (byte)reader.GetInt16(2), reader.GetInt32(3), reader.GetInt16(4), 0));
        }
        if (pending.Count == 0) return true;
        var toChar = unchecked((long)request.ToCharUid);
        var used = new HashSet<short>();
        using (var cmd = new NpgsqlCommand("SELECT item_pos FROM item_char WHERE char_uid = @c AND item_where = @w", conn, tx))
        {
            cmd.Parameters.AddWithValue("c", toChar);
            cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) used.Add(reader.GetInt16(0));
        }
        short NextPosition()
        {
            short pos = 0;
            while (used.Contains(pos)) pos++;
            used.Add(pos);
            return pos;
        }
        foreach (var item in pending)
        {
            var count = (ushort)Math.Max((short)0, item.Attr);
            long takenUid;
            if (item.Class == CommonClass)
            {
                long? stackUid = null;
                int stackCount = 0;
                using (var cmd = new NpgsqlCommand("""
                    SELECT i.item_uid, i.item_stack FROM items i JOIN item_char c ON c.item_uid = i.item_uid
                    WHERE c.char_uid = @c AND c.item_where = @w AND i.item_index = @i AND i.item_class = 1
                    ORDER BY c.item_pos LIMIT 1
                    """, conn, tx))
                {
                    cmd.Parameters.AddWithValue("c", toChar);
                    cmd.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
                    cmd.Parameters.AddWithValue("i", item.Index);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        stackUid = reader.GetInt64(0);
                        stackCount = reader.GetInt32(1);
                    }
                }
                if (stackUid is long existing)
                {
                    var total = stackCount + count;
                    using var update = new NpgsqlCommand("UPDATE items SET item_stack = @n WHERE item_uid = @u", conn, tx);
                    update.Parameters.AddWithValue("n", total);
                    update.Parameters.AddWithValue("u", existing);
                    update.ExecuteNonQuery();
                    takenUid = item.Uid;
                    received.Add(new PostReceivedItem(PostItemChange.Stacked, item.Seq, unchecked((ulong)existing), item.Index, CommonClass, count, (ushort)Math.Min(total, ushort.MaxValue), 0, 0, 0, 0));
                }
                else
                {
                    long created;
                    using (var insert = new NpgsqlCommand("""
                        INSERT INTO items (item_index, item_class, item_attr, item_charged, item_breaked, refine_type, refine_level, refine_state, item_slot, item_stack)
                        VALUES (@i, 1, 0, 0, '2020-01-01', 0, 0, 0, 0, @n)
                        RETURNING item_uid
                        """, conn, tx))
                    {
                        insert.Parameters.AddWithValue("i", item.Index);
                        insert.Parameters.AddWithValue("n", (int)count);
                        created = (long)insert.ExecuteScalar()!;
                    }
                    using (var own = new NpgsqlCommand("INSERT INTO item_char (char_uid, item_uid, item_where, item_pos) VALUES (@c, @u, @w, @pos)", conn, tx))
                    {
                        own.Parameters.AddWithValue("c", toChar);
                        own.Parameters.AddWithValue("u", created);
                        own.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
                        own.Parameters.AddWithValue("pos", NextPosition());
                        if (own.ExecuteNonQuery() != 1)
                        {
                            error = 111;
                            return false;
                        }
                    }
                    takenUid = created;
                    received.Add(new PostReceivedItem(PostItemChange.Created, item.Seq, unchecked((ulong)created), item.Index, CommonClass, count, count, 0, 0, 0, 0));
                }
            }
            else if (item.Uid > 0 && item.Class == RareClass)
            {
                using (var own = new NpgsqlCommand("""
                    INSERT INTO item_char (char_uid, item_uid, item_where, item_pos)
                    SELECT @c, @u, @w, @pos
                    WHERE EXISTS (SELECT 1 FROM items WHERE item_uid = @u)
                      AND NOT EXISTS (SELECT 1 FROM item_char WHERE item_uid = @u)
                    """, conn, tx))
                {
                    own.Parameters.AddWithValue("c", toChar);
                    own.Parameters.AddWithValue("u", item.Uid);
                    own.Parameters.AddWithValue("w", (short)SlotKind.Inventory);
                    own.Parameters.AddWithValue("pos", NextPosition());
                    if (own.ExecuteNonQuery() != 1)
                    {
                        error = 112;
                        return false;
                    }
                }
                using (var cmd = new NpgsqlCommand("SELECT item_stack, refine_type, refine_level, refine_state, item_slot FROM items WHERE item_uid = @u", conn, tx))
                {
                    cmd.Parameters.AddWithValue("u", item.Uid);
                    using var reader = cmd.ExecuteReader();
                    reader.Read();
                    var stack = (ushort)Math.Clamp(reader.GetInt32(0), 0, ushort.MaxValue);
                    received.Add(new PostReceivedItem(PostItemChange.Created, item.Seq, unchecked((ulong)item.Uid), item.Index, RareClass, stack, stack,
                        (byte)reader.GetInt16(1), (byte)reader.GetInt16(2), (byte)reader.GetInt16(3), (byte)reader.GetInt16(4)));
                }
                takenUid = item.Uid;
            }
            else
            {
                error = 113;
                return false;
            }
            using var taken = new NpgsqlCommand("""
                UPDATE post_item SET take_char_uid = @c, take_char_level = @l, take_at = NOW(), item_uid = @u
                WHERE item_seq = @s
                """, conn, tx);
            taken.Parameters.AddWithValue("c", toChar);
            taken.Parameters.AddWithValue("l", (short)Math.Clamp(request.ToCharLevel, short.MinValue, short.MaxValue));
            taken.Parameters.AddWithValue("u", takenUid);
            taken.Parameters.AddWithValue("s", item.Seq);
            if (taken.ExecuteNonQuery() != 1)
            {
                error = 115;
                return false;
            }
        }
        return true;
    }

    private static void Expire(NpgsqlConnection conn, NpgsqlTransaction tx, uint accountId)
    {
        using (var create = new NpgsqlCommand("""
            DROP TABLE IF EXISTS pg_temp.post_expiry;
            CREATE TEMP TABLE post_expiry (
                post_uid BIGINT, date_seq BIGINT, post_sub_type SMALLINT, post_state SMALLINT, new_post_state SMALLINT,
                user_type SMALLINT, account_id BIGINT, char_uid BIGINT, char_name VARCHAR(32), char_level SMALLINT,
                send_at TIMESTAMPTZ, return_at TIMESTAMPTZ,
                PRIMARY KEY (post_uid, new_post_state, account_id)
            ) ON COMMIT DROP;
            """, conn, tx))
        {
            create.ExecuteNonQuery();
        }
        int expired;
        using (var cmd = new NpgsqlCommand("""
            INSERT INTO post_expiry
            SELECT a.post_uid, a.date_seq, a.post_sub_type, a.post_state,
                   CASE WHEN a.post_sub_type <> 3 THEN CASE WHEN a.post_state = 0 THEN 2 ELSE 3 END ELSE 3 END,
                   a.user_type, a.account_id, a.char_uid, a.char_name, a.char_level, a.return_at,
                   CASE WHEN a.post_sub_type <> 3 THEN
                       CASE WHEN a.post_state = 0 THEN post_keep_until(a.return_at, 1::smallint)
                            ELSE post_keep_until(a.return_at, 0::smallint) END
                   ELSE post_keep_until(a.return_at, 0::smallint) END
            FROM (
                SELECT p.post_sub_type, d.date_seq, d.post_uid, d.post_state, d.user_type, d.account_id, d.char_uid,
                       d.char_name, d.char_level, d.send_at, d.return_at,
                       RANK() OVER (PARTITION BY d.post_uid ORDER BY d.date_seq DESC) AS rn
                FROM post p
                JOIN post_date d ON p.post_uid = d.post_uid AND p.post_state < 3
                WHERE d.account_id = @a
            ) a
            WHERE a.post_state IN (0, 1, 2) AND a.rn = 1 AND a.return_at <= NOW()
            """, conn, tx))
        {
            cmd.Parameters.AddWithValue("a", (long)accountId);
            expired = cmd.ExecuteNonQuery();
        }
        if (expired == 0) return;
        using var apply = new NpgsqlCommand("""
            INSERT INTO post_expiry
            SELECT post_uid, date_seq, post_sub_type, post_state, 3, user_type, account_id, char_uid, char_name, char_level,
                   return_at, post_keep_until(return_at, 0::smallint)
            FROM post_expiry WHERE new_post_state = 2 AND return_at <= NOW();

            INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
            SELECT a.post_uid, a.new_post_state, CASE WHEN b.user_type = 0 THEN 1 WHEN b.user_type = 1 THEN 0 END,
                   b.account_id, b.char_uid, b.char_name, b.char_level, b.send_at, a.return_at, NOW()
            FROM post_expiry a JOIN post_date b ON a.post_uid = b.post_uid AND a.post_state = b.post_state
            WHERE a.new_post_state = 2;

            INSERT INTO post_date (post_uid, post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, created_at)
            SELECT post_uid, new_post_state, user_type, account_id, char_uid, char_name, char_level, send_at, return_at, NOW()
            FROM post_expiry WHERE new_post_state = 3;

            UPDATE post a SET post_state = b.post_state
            FROM (
                SELECT b.post_uid, MAX(b.new_post_state) AS post_state
                FROM post_date a JOIN post_expiry b ON a.post_uid = b.post_uid AND a.post_state = b.post_state
                WHERE b.new_post_state >= 2
                GROUP BY b.post_uid HAVING COUNT(*) = 2
            ) b
            WHERE a.post_uid = b.post_uid;
            """, conn, tx);
        apply.ExecuteNonQuery();
    }
}
