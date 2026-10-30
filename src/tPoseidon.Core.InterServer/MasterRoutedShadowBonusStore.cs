using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedShadowBonusStore : IShadowBonusStore
{
    private readonly MasterShadow _master;
    private readonly IShadowBonusStore _direct;
    private readonly Action<string> _log;

    public MasterRoutedShadowBonusStore(MasterShadow master, IShadowBonusStore direct, Action<string> log)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _direct = direct ?? throw new ArgumentNullException(nameof(direct));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ShadowBonusRecord? Load(ulong characterId) => _direct.Load(characterId);

    public void Save(ShadowBonusRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!_master.Connected)
        {
            _log($"[shadow] Master not connected; result of 0x{record.CharacterId:X} written directly");
            _direct.Save(record);
            return;
        }
        _master.InsertResultAsync(record, CancellationToken.None).GetAwaiter().GetResult();
    }
}
