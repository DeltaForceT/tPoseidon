namespace tPoseidon.Core.Data;

public static class MegaphoneFlags
{
    public const byte Waiting = 0;
    public const byte Queued = 1;
    public const byte Delivered = 2;
    public const byte InterruptedRefunded = 3;
    public const byte InterruptedBagFull = 4;

    public static bool IsPending(byte flag) => flag <= Queued;
}

public static class MegaphoneKinds
{
    public const ulong NoticeSender = 0;
    public const byte Notice = 0;
    public const byte Normal = 1;
}

public static class MegaphoneRules
{
    public const string DelayKey = "MegaphoneDelay";
    public const int DefaultDelayMilliseconds = 18000;
    public const int MaxMessageBytes = 300;

    public static int DelayFromIni(int configured) => configured < 0 ? DefaultDelayMilliseconds : configured;
}

public static class MegaphoneErrors
{
    public const uint InvalidCharacter = 0;
    public const uint Fault = 2;
    public const uint StoreFailure = 101;
}

public readonly record struct MegaphoneRefund(int ItemIndex, byte ItemClass);

public sealed record QueuedMegaphone(long Code, ulong CharacterId, string Message, byte Kind, byte Flag, MegaphoneRefund Refund);

public interface IMegaphoneQueueStore
{
    int Enqueue(ulong characterId, string message, MegaphoneRefund refund);

    int EnqueueNotice(string message);

    QueuedMegaphone? NextPending();

    bool MarkDelivered(long code);

    IReadOnlyList<QueuedMegaphone> Interrupted();

    void Settle(long code, byte flag);
}
