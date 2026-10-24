using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Kr2014.Wire;

[WireOpcode((ushort)Opcode.PLAYER_SPAWN_LOGIN)]
[WireOpcode((ushort)Opcode.S2C_PLAYER_SPAWN_POST_WARP)]
public sealed class PlayerSpawnLoginPacket
{
    public const int HeaderBytes = 0x16;
    public const int LookMaskOffset = 26;
    public const int LookRestBytes = 29;
    public const uint EarAndTailBits = 0x60;
    public const int GmFlagsOffset = 20;
    public const byte GmEagleFlag = 0x01;
    public const byte GmEagleWingsFlag = 0x08;
    public const int CompactFooterBytes = LookMaskOffset + 4 + 2 * 4 + 4 + LookRestBytes;

    private static readonly (int Bit, byte Slot)[] LookBits =
    {
        (0, EquipmentSlot.Hat), (1, EquipmentSlot.Weapon), (2, EquipmentSlot.Shield),
        (5, EquipmentSlot.Ear), (6, EquipmentSlot.Tail),
        (7, 10), (8, 11), (9, 12), (10, 13), (11, 14), (12, 15), (13, 16), (14, 17),
        (15, EquipmentSlot.Shoes), (16, EquipmentSlot.Socks),
        (18, EquipmentSlot.Drill), (19, EquipmentSlot.Cape),
    };

    private static readonly (int Bit, byte Slot)[] LookOverrideBits =
    {
        (0, EquipmentSlot.Hat), (1, EquipmentSlot.Weapon), (2, EquipmentSlot.Shield),
        (12, 15), (13, 16), (19, EquipmentSlot.Cape),
    };

    public ushort EntityId { get; init; }
    public ushort AppearanceType { get; init; } = 1;
    public ushort BodyType { get; init; }
    public byte Hair { get; init; }
    public byte ClassChainId { get; init; }
    public ushort Level { get; init; } = 1;
    public uint UnknownA { get; init; } = 8;
    public ushort PositionX { get; init; }
    public ushort PositionY { get; init; }
    public ushort RespawnX { get; init; }
    public ushort RespawnY { get; init; }
    public string Name { get; init; } = string.Empty;
    public byte EncodedYaw { get; init; }
    public byte SpeedQuarter { get; init; }
    public uint UnknownB { get; init; } = 1;
    public uint ClassChainMirror { get; init; }
    public byte GmFlags { get; init; }
    public byte PostureCode { get; init; }

    public IReadOnlyDictionary<byte, uint> LookItems { get; init; } = new Dictionary<byte, uint>();

    public IReadOnlyDictionary<byte, uint> LookOverrides { get; init; } = new Dictionary<byte, uint>();

    public static PlayerSpawnLoginPacket FromCharacter(
        Character character,
        ushort entityId,
        IItemStore? itemStore = null)
        => FromCharacter(character, entityId, itemStore, null);

    public static PlayerSpawnLoginPacket FromCharacter(
        Character character,
        ushort entityId,
        IItemStore? itemStore,
        (ushort X, ushort Y)? position,
        byte speedQuarter = 0,
        byte gmFlags = 0,
        byte postureCode = 0)
    {
        ArgumentNullException.ThrowIfNull(character);
        var speciesId = (byte)character.Species;

        var looks = new Dictionary<byte, uint>
        {
            [EquipmentSlot.Ear] = character.StarterEarItemIndex,
            [EquipmentSlot.Tail] = character.StarterTailItemIndex,
        };
        if (itemStore is not null)
        {
            foreach (var item in itemStore.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment))
            {
                if (Array.Exists(LookBits, look => look.Slot == item.SlotPosition)) looks[item.SlotPosition] = (uint)item.ItemIndex;
            }
        }

        return new PlayerSpawnLoginPacket
        {
            EntityId = entityId,
            BodyType = speciesId,
            Hair = (byte)character.Hair,
            ClassChainId = speciesId,
            Level = character.Level,
            PositionX = position?.X ?? character.PositionX,
            PositionY = position?.Y ?? character.PositionY,
            RespawnX = position?.X ?? character.PositionX,
            RespawnY = position?.Y ?? character.PositionY,
            Name = character.Name,
            SpeedQuarter = speedQuarter,
            ClassChainMirror = speciesId,
            GmFlags = gmFlags,
            PostureCode = postureCode,
            LookItems = looks,
        };
    }

    public uint LookMask => MaskOf(LookBits, LookItems) | EarAndTailBits;

    public uint LookOverrideMask => MaskOf(LookOverrideBits, LookOverrides);

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Name);
        var nameBytes = Encoding.ASCII.GetBytes(Name);
        var lookMask = LookMask;
        var overrideMask = LookOverrideMask;
        var footerLen = LookMaskOffset + 4 + 4 * CountBits(lookMask) + 4 + 4 * CountBits(overrideMask) + LookRestBytes;
        var buf = new byte[HeaderBytes + nameBytes.Length + 1 + footerLen];

        WriteUInt16Le(buf, 0x00, EntityId);
        WriteUInt16Le(buf, 0x02, AppearanceType);
        WriteUInt16Le(buf, 0x04, BodyType);
        buf[0x06] = Hair;
        buf[0x07] = ClassChainId;
        WriteUInt16Le(buf, 0x08, Level);
        WriteUInt32Le(buf, 0x0A, UnknownA);
        WriteUInt16Le(buf, 0x0E, PositionX);
        WriteUInt16Le(buf, 0x10, PositionY);
        WriteUInt16Le(buf, 0x12, RespawnX);
        WriteUInt16Le(buf, 0x14, RespawnY);

        Array.Copy(nameBytes, 0, buf, 0x16, nameBytes.Length);

        var f = HeaderBytes + nameBytes.Length + 1;
        buf[f] = PostureCode;
        buf[f + 1] = EncodedYaw;
        buf[f + 2] = SpeedQuarter;
        WriteUInt32Le(buf, f + 7, UnknownB);
        buf[f + GmFlagsOffset] = GmFlags;
        WriteUInt32Le(buf, f + 21, ClassChainMirror);

        var at = f + LookMaskOffset;
        at = WriteLooks(buf, at, LookBits, lookMask, LookItems);
        at = WriteLooks(buf, at, LookOverrideBits, overrideMask, LookOverrides);
        buf[at] = Hair;
        return buf;
    }

    public static PlayerSpawnLoginPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderBytes + 1 + LookMaskOffset + 4)
        {
            throw new InvalidPacketException(
                $"Kr2014 PlayerSpawnLogin payload {payload.Length}B too short for header and look mask.");
        }
        var nul = payload[HeaderBytes..].IndexOf((byte)0);
        if (nul < 0)
        {
            throw new InvalidPacketException(
                "Kr2014 PlayerSpawnLogin name field has no NUL terminator.");
        }
        var name = nul == 0 ? string.Empty : Encoding.ASCII.GetString(payload.Slice(HeaderBytes, nul));
        var f = HeaderBytes + nul + 1;
        var at = f + LookMaskOffset;
        var looks = ReadLooks(payload, ref at, LookBits);
        var overrides = ReadLooks(payload, ref at, LookOverrideBits);
        if (payload.Length - at != LookRestBytes)
        {
            throw new InvalidPacketException(
                $"Kr2014 PlayerSpawnLogin payload {payload.Length}B leaves {payload.Length - at}B after the look blocks, expected {LookRestBytes}B.");
        }

        return new PlayerSpawnLoginPacket
        {
            EntityId = ReadUInt16Le(payload, 0x00),
            AppearanceType = ReadUInt16Le(payload, 0x02),
            BodyType = ReadUInt16Le(payload, 0x04),
            Hair = payload[0x06],
            ClassChainId = payload[0x07],
            Level = ReadUInt16Le(payload, 0x08),
            UnknownA = ReadUInt32Le(payload, 0x0A),
            PositionX = ReadUInt16Le(payload, 0x0E),
            PositionY = ReadUInt16Le(payload, 0x10),
            RespawnX = ReadUInt16Le(payload, 0x12),
            RespawnY = ReadUInt16Le(payload, 0x14),
            Name = name,
            PostureCode = payload[f],
            EncodedYaw = payload[f + 1],
            SpeedQuarter = payload[f + 2],
            UnknownB = ReadUInt32Le(payload, f + 7),
            ClassChainMirror = ReadUInt32Le(payload, f + 21),
            GmFlags = payload[f + GmFlagsOffset],
            LookItems = looks,
            LookOverrides = overrides,
        };
    }

    private static uint MaskOf((int Bit, byte Slot)[] bits, IReadOnlyDictionary<byte, uint> items)
    {
        var mask = 0u;
        foreach (var (bit, slot) in bits)
        {
            if (items.TryGetValue(slot, out var itemId) && itemId != 0) mask |= 1u << bit;
        }
        return mask;
    }

    private static int CountBits(uint mask) => System.Numerics.BitOperations.PopCount(mask);

    private static int WriteLooks(byte[] buf, int at, (int Bit, byte Slot)[] bits, uint mask, IReadOnlyDictionary<byte, uint> items)
    {
        WriteUInt32Le(buf, at, mask);
        at += 4;
        foreach (var (bit, slot) in bits)
        {
            if ((mask & (1u << bit)) == 0) continue;
            WriteUInt32Le(buf, at, items.TryGetValue(slot, out var itemId) ? itemId : 0);
            at += 4;
        }
        return at;
    }

    private static Dictionary<byte, uint> ReadLooks(ReadOnlySpan<byte> payload, ref int at, (int Bit, byte Slot)[] bits)
    {
        if (payload.Length < at + 4)
        {
            throw new InvalidPacketException($"Kr2014 PlayerSpawnLogin payload {payload.Length}B ends inside a look mask at {at}.");
        }
        var mask = ReadUInt32Le(payload, at);
        at += 4;
        var known = 0u;
        foreach (var (bit, _) in bits) known |= 1u << bit;
        if ((mask & ~known) != 0)
        {
            throw new InvalidPacketException($"Kr2014 PlayerSpawnLogin look mask 0x{mask:X} has bits the client does not read.");
        }
        var items = new Dictionary<byte, uint>();
        foreach (var (bit, slot) in bits)
        {
            if ((mask & (1u << bit)) == 0) continue;
            if (payload.Length < at + 4)
            {
                throw new InvalidPacketException($"Kr2014 PlayerSpawnLogin payload {payload.Length}B ends inside the look items at {at}.");
            }
            items[slot] = ReadUInt32Le(payload, at);
            at += 4;
        }
        return items;
    }

    private static void WriteUInt16Le(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static ushort ReadUInt16Le(ReadOnlySpan<byte> buf, int offset) =>
        (ushort)(buf[offset] | (buf[offset + 1] << 8));

    private static uint ReadUInt32Le(ReadOnlySpan<byte> buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));
}
