namespace tPoseidon.Core.Crypto.S2;

public sealed class SessionInfo
{
        public byte RecvKey { get; set; }

        public byte SendKey { get; set; }

        public byte Key
    {
        get => RecvKey;
        set => RecvKey = value;
    }

        public bool IsFirstPacket { get; set; } = true;

        public bool IsStrict { get; set; } = true;

        public object Lock { get; } = new();

    public SessionInfo(byte initialKey = 0x01)
    {
        RecvKey = initialKey;
        SendKey = initialKey;
    }
}
