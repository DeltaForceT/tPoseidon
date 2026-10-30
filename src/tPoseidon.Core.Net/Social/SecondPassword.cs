using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Social;

public static class SecondPasswordLock
{
    public const byte Empty = 0;
    public const byte Disabled = 1;
    public const byte Unlocked = 2;
    public const byte Locked = 3;
}

public static class SecondPasswordCodes
{
    public const byte WrongPassword = 1;
    public const byte WrongPreviousPassword = 2;
    public const byte WrongLength = 3;
    public const byte FailedFromDatabase = 4;
    public const byte WrongAction = 5;
}

public static class SecondPasswordGuards
{
    public const byte BankAndStorage = 1;
    public const byte ShopsAndTrade = 2;
    public const byte MyShopAndGuild = 4;
    public const byte All = BankAndStorage | ShopsAndTrade | MyShopAndGuild;
}

public static class SecondPasswordGuardTable
{
    private static readonly Dictionary<ushort, byte> Guards = new()
    {
        [0x02FE] = SecondPasswordGuards.BankAndStorage,
        [0x0043] = SecondPasswordGuards.ShopsAndTrade,
        [0x004C] = SecondPasswordGuards.ShopsAndTrade,
        [0x00AC] = SecondPasswordGuards.ShopsAndTrade,
        [0x00B0] = SecondPasswordGuards.ShopsAndTrade,
        [0x015B] = SecondPasswordGuards.ShopsAndTrade,
        [0x0163] = SecondPasswordGuards.ShopsAndTrade,
        [0x03F1] = SecondPasswordGuards.ShopsAndTrade,
        [0x03F5] = SecondPasswordGuards.ShopsAndTrade,
        [0x03F7] = SecondPasswordGuards.ShopsAndTrade,
        [0x04D4] = SecondPasswordGuards.ShopsAndTrade,
        [0x04D7] = SecondPasswordGuards.ShopsAndTrade,
        [0x04DA] = SecondPasswordGuards.ShopsAndTrade,
    };

    private static readonly Dictionary<uint, byte> NpcFunctionGuards = new()
    {
        [0x10] = SecondPasswordGuards.BankAndStorage,
        [0x11] = SecondPasswordGuards.BankAndStorage,
        [0x35] = SecondPasswordGuards.MyShopAndGuild,
        [0x36] = SecondPasswordGuards.MyShopAndGuild,
        [0x4F] = SecondPasswordGuards.MyShopAndGuild,
        [0x50] = SecondPasswordGuards.MyShopAndGuild,
        [0x51] = SecondPasswordGuards.MyShopAndGuild,
    };

    public static byte GuardOf(ushort innerOpcode) => Guards.GetValueOrDefault(innerOpcode);

    public static byte GuardOfNpcFunction(uint functionId) => NpcFunctionGuards.GetValueOrDefault(functionId);
}

public sealed record SecondPasswordReply(ushort Opcode, byte[] Payload);

public static class SecondPasswordFrames
{
    public const ushort Envelope = 0x039F;
    public const ushort SetRequest = 0x0801;
    public const ushort ChangeRequest = 0x0802;
    public const ushort StateRequest = 0x0805;
    public const ushort Info = 0x0803;
    public const ushort Failed = 0x0804;
    public const ushort StateChanged = 0x0806;
    public const ushort Accepted = 0x03A0;
    public const ushort Refused = 0x03A1;
    public const int HashLength = 16;

    public static SecondPasswordReply AcceptedReply() => new(Accepted, BitConverter.GetBytes(Accepted));

    public static SecondPasswordReply RefusedReply(byte code) => new(Refused, BitConverter.GetBytes((uint)code));

    public static SecondPasswordReply FailedReply(byte code) => new(Failed, BitConverter.GetBytes((uint)code));

    public static SecondPasswordReply StateChangedReply(byte state) => new(StateChanged, BitConverter.GetBytes((uint)state));

    public static SecondPasswordReply InfoReply(string email, byte lockFlag, byte bitmask, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(email.Length + 1 + 2 * sizeof(uint));
        w.WriteNulTerminated(email, text);
        w.WriteUInt32(lockFlag);
        w.WriteUInt32(bitmask);
        return new SecondPasswordReply(Info, w.ToArray());
    }
}

public sealed class SecondPasswordService
{
    private readonly ISafetyPasswordStore _store;
    private readonly Encoding _text;
    private readonly TimeProvider _time;

    public SecondPasswordService(ISafetyPasswordStore store, Encoding text, TimeProvider time)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public IReadOnlyList<SecondPasswordReply> InfoAtLogin(uint accountId) =>
        _store.Find(accountId) is { } record
            ? [SecondPasswordFrames.InfoReply(record.Email, record.LockFlag, record.LockLimitBitmask, _text)]
            : [];

    public bool PermitsCharacterDelete(uint accountId, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(passwordHash);
        return _store.Find(accountId) is not { LockFlag: SecondPasswordLock.Locked } record
            || string.Equals(record.PasswordHash, passwordHash, StringComparison.Ordinal);
    }

    public bool PermitsCommand(uint accountId, ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < sizeof(ushort)) return false;
        var inner = BinaryPrimitives.ReadUInt16LittleEndian(envelope);
        var rest = envelope[sizeof(ushort)..];
        var passwordRead = TryReadText(ref rest, out var password);
        var guard = inner == NpcScriptRequest.InnerOpcode
            ? passwordRead && NpcFunctionOf(rest) is { } function ? SecondPasswordGuardTable.GuardOfNpcFunction(function) : (byte)0
            : SecondPasswordGuardTable.GuardOf(inner);
        if (guard == 0) return true;
        if (_store.Find(accountId) is not { LockFlag: SecondPasswordLock.Locked } record || (record.LockLimitBitmask & guard) == 0) return true;
        return passwordRead && string.Equals(record.PasswordHash, password, StringComparison.Ordinal);
    }

    public IReadOnlyList<SecondPasswordReply> Handle(uint accountId, ReadOnlySpan<byte> envelope)
    {
        if (envelope.Length < sizeof(ushort)) return [];
        var inner = BinaryPrimitives.ReadUInt16LittleEndian(envelope);
        var rest = envelope[sizeof(ushort)..];
        if (!TryReadText(ref rest, out var password)) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        return inner switch
        {
            SecondPasswordFrames.SetRequest => Set(accountId, password, rest),
            SecondPasswordFrames.ChangeRequest => Change(accountId, password, rest),
            SecondPasswordFrames.StateRequest => SetState(accountId, password, rest),
            _ => [],
        };
    }

    private IReadOnlyList<SecondPasswordReply> Set(uint accountId, string passwordHash, ReadOnlySpan<byte> rest)
    {
        if (!TryReadText(ref rest, out var email)) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        if (StateOf(_store.Find(accountId)) != SecondPasswordLock.Empty) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        if (passwordHash.Length != SecondPasswordFrames.HashLength) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongLength)];
        var now = _time.GetUtcNow();
        _store.Upsert(new SafetyPassword
        {
            AccountId = accountId,
            PasswordHash = passwordHash,
            LockFlag = SecondPasswordLock.Locked,
            LockLimitBitmask = SecondPasswordGuards.All,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now,
        });
        Log(SafetyPasswordLogType.Created, accountId, passwordHash, SecondPasswordLock.Locked, SecondPasswordGuards.All, email, now);
        return
        [
            SecondPasswordFrames.AcceptedReply(),
            SecondPasswordFrames.InfoReply(email, SecondPasswordLock.Locked, SecondPasswordGuards.All, _text),
        ];
    }

    private IReadOnlyList<SecondPasswordReply> Change(uint accountId, string previousHash, ReadOnlySpan<byte> rest)
    {
        if (!TryReadText(ref rest, out var newHash) || !TryReadText(ref rest, out var email))
        {
            return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        }
        if (_store.Find(accountId) is not { } record || StateOf(record) is SecondPasswordLock.Empty or SecondPasswordLock.Disabled)
        {
            return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        }
        if (!string.Equals(record.PasswordHash, previousHash, StringComparison.Ordinal)) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongPreviousPassword)];
        if (newHash.Length != SecondPasswordFrames.HashLength) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongLength)];
        var now = _time.GetUtcNow();
        var kept = email.Length == 0 ? record.Email : email;
        _store.Upsert(record with { PasswordHash = newHash, Email = kept, UpdatedAt = now });
        Log(SafetyPasswordLogType.Updated, accountId, newHash, record.LockFlag, record.LockLimitBitmask, kept, now);
        return
        [
            SecondPasswordFrames.AcceptedReply(),
            SecondPasswordFrames.InfoReply(kept, record.LockFlag, record.LockLimitBitmask, _text),
        ];
    }

    private IReadOnlyList<SecondPasswordReply> SetState(uint accountId, string passwordHash, ReadOnlySpan<byte> rest)
    {
        if (rest.Length < sizeof(uint)) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        var target = BinaryPrimitives.ReadUInt32LittleEndian(rest);
        if (target is < SecondPasswordLock.Disabled or > SecondPasswordLock.Locked) return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongAction)];
        var record = _store.Find(accountId);
        var current = StateOf(record);
        if (current == target) return [SecondPasswordFrames.AcceptedReply()];
        if (target == SecondPasswordLock.Unlocked && current != SecondPasswordLock.Locked) return [SecondPasswordFrames.AcceptedReply()];
        var needsPassword = !(target == SecondPasswordLock.Disabled && current == SecondPasswordLock.Empty);
        if (needsPassword && !string.Equals(record?.PasswordHash ?? string.Empty, passwordHash, StringComparison.Ordinal))
        {
            return [SecondPasswordFrames.RefusedReply(SecondPasswordCodes.WrongPassword)];
        }
        if (record is null || !_store.SetLockFlag(accountId, (byte)target))
        {
            return [SecondPasswordFrames.AcceptedReply(), SecondPasswordFrames.FailedReply(SecondPasswordCodes.FailedFromDatabase)];
        }
        Log(SafetyPasswordLogType.LockFlagChanged, accountId, record.PasswordHash, (byte)target, record.LockLimitBitmask, record.Email, _time.GetUtcNow());
        return [SecondPasswordFrames.AcceptedReply(), SecondPasswordFrames.StateChangedReply((byte)target)];
    }

    private void Log(SafetyPasswordLogType type, uint accountId, string hash, byte lockFlag, byte bitmask, string email, DateTimeOffset now) =>
        _store.AppendLog(new SafetyPasswordLogEntry
        {
            LogType = type,
            AccountId = accountId,
            PasswordHash = hash,
            LockFlag = lockFlag,
            LockLimitBitmask = bitmask,
            Email = email,
            CreatedAt = now,
        });

    private static uint? NpcFunctionOf(ReadOnlySpan<byte> body) =>
        body.Length < NpcScriptRequest.CallHeaderSize ? null : BinaryPrimitives.ReadUInt32LittleEndian(body[sizeof(ushort)..]);

    private static byte StateOf(SafetyPassword? record) => record?.LockFlag ?? SecondPasswordLock.Empty;

    private bool TryReadText(ref ReadOnlySpan<byte> rest, out string text)
    {
        var end = rest.IndexOf((byte)0);
        if (end < 0)
        {
            text = string.Empty;
            return false;
        }
        text = _text.GetString(rest[..end]);
        rest = rest[(end + 1)..];
        return true;
    }
}
