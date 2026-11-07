namespace tPoseidon.Core.Net.World;

public enum TradeParty
{
    Inviter,
    Invitee,
}

public enum TradeExecution
{
    Ignored,
    Cancelled,
    Commit,
}

public readonly record struct TradeCheckView(bool OwnChecked, bool PartnerChecked, bool OwnIsFirstChecker);

public readonly record struct TradeExecutionResult(TradeExecution Outcome, TradeParty Executor);

public sealed class TradeExchange
{
    private readonly bool[] _checked = new bool[PartyCount];
    private readonly bool[] _locked = new bool[PartyCount];
    private const int PartyCount = 2;

    public TradeParty? FirstChecker { get; private set; }

    public static TradeParty Opposite(TradeParty party) => party == TradeParty.Inviter ? TradeParty.Invitee : TradeParty.Inviter;

    public bool IsChecked(TradeParty party) => _checked[(int)party];

    public bool IsLocked(TradeParty party) => _locked[(int)party];

    public void SetChecked(TradeParty party, bool isChecked)
    {
        var opposite = Opposite(party);
        _checked[(int)party] = isChecked;
        if (!isChecked)
        {
            _checked[(int)opposite] = false;
            FirstChecker = null;
        }
        else if (!_checked[(int)opposite])
        {
            FirstChecker = party;
        }
    }

    public TradeCheckView CheckViewFor(TradeParty recipient) =>
        new(_checked[(int)recipient], _checked[(int)Opposite(recipient)], FirstChecker == recipient);

    public bool MayProceedToConfirm(TradeParty party) => FirstChecker == party;

    public void Lock(TradeParty party) => _locked[(int)party] = true;

    public void ReleaseLocks()
    {
        _locked[(int)TradeParty.Inviter] = false;
        _locked[(int)TradeParty.Invitee] = false;
    }

    public TradeExecutionResult Execute(TradeParty sender)
    {
        var executor = Opposite(sender);
        if (_locked[(int)sender]) return new TradeExecutionResult(TradeExecution.Ignored, executor);
        _locked[(int)sender] = true;
        var commit = _locked[(int)executor] && _checked[(int)sender] && _checked[(int)executor] && FirstChecker == executor;
        return new TradeExecutionResult(commit ? TradeExecution.Commit : TradeExecution.Cancelled, executor);
    }
}
