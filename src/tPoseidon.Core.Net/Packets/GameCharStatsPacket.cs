using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_CHAR_STATS)]
public sealed class GameCharStatsPacket
{
    public const int PayloadSize = 278;

    public const int EquipSlotCount = 23;
    public const int EquipSlotBytes = 8;
    public const int EquipBlockOffset = 0x00;

    public const int AccPtsBlockOffset = 0xB8;

    public EquipSlot[] EquipSlots { get; init; } = new EquipSlot[EquipSlotCount];

    public ushort Ap { get; init; }
    public ushort Ac { get; init; }
    public ushort Dx { get; init; }
    public ushort MaxMp { get; init; }
    public ushort Ma { get; init; }
    public ushort Md { get; init; }
    public ushort MaxWt { get; init; }
    public ushort Da { get; init; }
    public ushort Lk { get; init; }
    public ushort MaxHp { get; init; }
    public ushort Dp { get; init; }
    public ushort Hv { get; init; }

    public ushort AttrPts { get; init; }
    public ushort TmLevel { get; init; }
    public ushort TmPoint { get; init; }
    public byte Level { get; init; }

    public byte EntityMarker { get; init; } = 1;
    public ushort MapId { get; init; }
    public ushort Exp { get; init; }
    public ushort OpaqueAt0xDFTbd { get; init; }
    public ushort NextLevelExp { get; init; }
    public ushort OpaqueAt0xE3Tbd { get; init; }
    public ushort MapIdMirror { get; init; }
    public uint Money { get; init; }
    public ushort MaxHpDerived { get; init; }
    public ushort CurrentHp { get; init; }
    public ushort MaxMpDerived { get; init; }
    public ushort CurrentMp { get; init; }
    public uint TmExp { get; init; }
    public uint CurrentLevelExp { get; init; }
    public ushort OpaqueAt0x10BTbd { get; init; }
    public byte Hair { get; init; }
    public byte EntityMarker2 { get; init; } = 1;
    public uint OpaqueAt0x112Tbd { get; init; } = 80;

    public sealed class EquipSlot
    {
        public uint ItemUid { get; init; }
        public uint Flags { get; init; }
    }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];

        if (EquipSlots is null || EquipSlots.Length != EquipSlotCount)
        {
            throw new InvalidOperationException(
                $"EquipSlots must have exactly {EquipSlotCount} entries, got {EquipSlots?.Length ?? 0}.");
        }

        for (var i = 0; i < EquipSlotCount; i++)
        {
            var off = EquipBlockOffset + i * EquipSlotBytes;
            var slot = EquipSlots[i];
            if (slot is null) continue;
            WriteUInt32Le(buf, off, slot.ItemUid);
            WriteUInt32Le(buf, off + 4, slot.Flags);
        }

        var a = AccPtsBlockOffset;
        WriteUInt16Le(buf, a + 0,  Ap);
        WriteUInt16Le(buf, a + 2,  Ac);
        WriteUInt16Le(buf, a + 4,  Dx);
        WriteUInt16Le(buf, a + 6,  MaxMp);
        WriteUInt16Le(buf, a + 8,  Ma);
        WriteUInt16Le(buf, a + 10, Md);
        WriteUInt16Le(buf, a + 12, MaxWt);
        WriteUInt16Le(buf, a + 14, Da);
        WriteUInt16Le(buf, a + 16, Lk);
        WriteUInt16Le(buf, a + 18, MaxHp);
        WriteUInt16Le(buf, a + 20, Dp);
        WriteUInt16Le(buf, a + 22, Hv);

        WriteUInt16Le(buf, 0xD0, AttrPts);
        WriteUInt16Le(buf, 0xD2, TmLevel);
        WriteUInt16Le(buf, 0xD4, TmPoint);
        buf[0xD6] = Level;

        buf[0xD9] = EntityMarker;
        WriteUInt16Le(buf, 0xDB, MapId);
        WriteUInt16Le(buf, 0xDD, Exp);
        WriteUInt16Le(buf, 0xDF, OpaqueAt0xDFTbd);
        WriteUInt16Le(buf, 0xE1, NextLevelExp);
        WriteUInt16Le(buf, 0xE3, OpaqueAt0xE3Tbd);
        WriteUInt16Le(buf, 0xE5, MapIdMirror);
        WriteUInt32Le(buf, 0xE7, Money);
        WriteUInt16Le(buf, 0xEB, MaxHpDerived);
        WriteUInt16Le(buf, 0xED, CurrentHp);
        WriteUInt16Le(buf, 0xEF, MaxMpDerived);
        WriteUInt16Le(buf, 0xF1, CurrentMp);
        WriteUInt32Le(buf, 0xF3, TmExp);
        WriteUInt32Le(buf, 0xF7, CurrentLevelExp);

        WriteUInt16Le(buf, 0x10B, OpaqueAt0x10BTbd);
        buf[0x10F] = Hair;
        buf[0x111] = EntityMarker2;
        WriteUInt32Le(buf, 0x112, OpaqueAt0x112Tbd);

        return buf;
    }

    public static GameCharStatsPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GameCharStats payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }

        var slots = new EquipSlot[EquipSlotCount];
        for (var i = 0; i < EquipSlotCount; i++)
        {
            var off = EquipBlockOffset + i * EquipSlotBytes;
            slots[i] = new EquipSlot
            {
                ItemUid = ReadUInt32Le(payload, off),
                Flags = ReadUInt32Le(payload, off + 4),
            };
        }

        var a = AccPtsBlockOffset;
        return new GameCharStatsPacket
        {
            EquipSlots = slots,
            Ap     = ReadUInt16Le(payload, a + 0),
            Ac     = ReadUInt16Le(payload, a + 2),
            Dx     = ReadUInt16Le(payload, a + 4),
            MaxMp  = ReadUInt16Le(payload, a + 6),
            Ma     = ReadUInt16Le(payload, a + 8),
            Md     = ReadUInt16Le(payload, a + 10),
            MaxWt  = ReadUInt16Le(payload, a + 12),
            Da     = ReadUInt16Le(payload, a + 14),
            Lk     = ReadUInt16Le(payload, a + 16),
            MaxHp  = ReadUInt16Le(payload, a + 18),
            Dp     = ReadUInt16Le(payload, a + 20),
            Hv     = ReadUInt16Le(payload, a + 22),
            AttrPts = ReadUInt16Le(payload, 0xD0),
            TmLevel = ReadUInt16Le(payload, 0xD2),
            TmPoint = ReadUInt16Le(payload, 0xD4),
            Level   = payload[0xD6],
            EntityMarker     = payload[0xD9],
            MapId            = ReadUInt16Le(payload, 0xDB),
            Exp              = ReadUInt16Le(payload, 0xDD),
            OpaqueAt0xDFTbd = ReadUInt16Le(payload, 0xDF),
            NextLevelExp     = ReadUInt16Le(payload, 0xE1),
            OpaqueAt0xE3Tbd = ReadUInt16Le(payload, 0xE3),
            MapIdMirror      = ReadUInt16Le(payload, 0xE5),
            Money            = ReadUInt32Le(payload, 0xE7),
            MaxHpDerived     = ReadUInt16Le(payload, 0xEB),
            CurrentHp        = ReadUInt16Le(payload, 0xED),
            MaxMpDerived     = ReadUInt16Le(payload, 0xEF),
            CurrentMp        = ReadUInt16Le(payload, 0xF1),
            TmExp            = ReadUInt32Le(payload, 0xF3),
            CurrentLevelExp  = ReadUInt32Le(payload, 0xF7),
            OpaqueAt0x10BTbd = ReadUInt16Le(payload, 0x10B),
            Hair = payload[0x10F],
            EntityMarker2     = payload[0x111],
            OpaqueAt0x112Tbd = ReadUInt32Le(payload, 0x112),
        };
    }

    private static void WriteUInt32Le(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteUInt16Le(byte[] buf, int offset, ushort value)
    {
        buf[offset] = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static uint ReadUInt32Le(ReadOnlySpan<byte> buf, int offset) =>
        (uint)(buf[offset] | (buf[offset + 1] << 8) | (buf[offset + 2] << 16) | (buf[offset + 3] << 24));

    private static ushort ReadUInt16Le(ReadOnlySpan<byte> buf, int offset) =>
        (ushort)(buf[offset] | (buf[offset + 1] << 8));
}
