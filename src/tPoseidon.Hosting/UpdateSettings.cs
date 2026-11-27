using tPoseidon.Hosting.Updates;

namespace tPoseidon.Hosting;

public enum UpdateMode
{
    NotifyOnly,
    OneClick,
    Automatic,
}

public sealed class UpdateSettings
{
    public const int DefaultCountdownMinutes = 5;
    public const int MaxCountdownMinutes = 120;
    public const int DefaultAutomaticHour = 4;

    public UpdateMode Mode { get; set; } = UpdateMode.OneClick;

    public ReleaseChannel Channel { get; set; } = ReleaseChannel.Beta;

    public int AutomaticHour { get; set; } = DefaultAutomaticHour;

    public int CountdownMinutes { get; set; } = DefaultCountdownMinutes;

    public string NoticeCharacter { get; set; } = "";

    public string NoticePassword { get; set; } = "";

    public string FeedUrl { get; set; } = "";

    public bool CanSendNotice => NoticeCharacter.Length > 0 && NoticePassword.Length > 0;

    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (AutomaticHour is < 0 or > 23)
        {
            problems.Add($"updates.automaticHour is {AutomaticHour}; use a whole number from 0 to 23.");
        }

        if (CountdownMinutes is < 0 or > MaxCountdownMinutes)
        {
            problems.Add($"updates.countdownMinutes is {CountdownMinutes}; use a whole number from 0 to {MaxCountdownMinutes}.");
        }

        if (FeedUrl.Length > 0 && !Uri.TryCreate(FeedUrl, UriKind.Absolute, out _))
        {
            problems.Add("updates.feedUrl is not a web address.");
        }

        return problems;
    }
}
