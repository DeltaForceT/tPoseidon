using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Notices;

public sealed class ServerNoticeTexts
{
    public const string ChangeJobTable = "ChangeJobStrings";
    public const string PrefixRow = "Trickster";
    public const string JobChangeRow = "CJNotice";
    public const string AwakenTable = "AwakenStrings";
    public const string AwakenRow = "MsgAwakenComlete2";
    public const byte AnnouncedJobTier = OccupationRules.ThirdJobTier;
    public const byte AnnouncedGuardianSlot = 0;

    private readonly IReadOnlyDictionary<string, string> _changeJob;
    private readonly IReadOnlyDictionary<string, string> _awaken;
    private readonly IReadOnlyDictionary<int, string> _jobNames;

    public ServerNoticeTexts(
        IReadOnlyDictionary<string, string> changeJob,
        IReadOnlyDictionary<string, string> awaken,
        IReadOnlyDictionary<int, string> jobNames)
    {
        _changeJob = changeJob ?? throw new ArgumentNullException(nameof(changeJob));
        _awaken = awaken ?? throw new ArgumentNullException(nameof(awaken));
        _jobNames = jobNames ?? throw new ArgumentNullException(nameof(jobNames));
    }

    public SystemNotice? JobChanged(byte tier, ushort characterType, string playerName)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        if (tier != AnnouncedJobTier) return null;
        if (!_changeJob.TryGetValue(PrefixRow, out var prefix) || !_changeJob.TryGetValue(JobChangeRow, out var template)) return null;
        if (!_jobNames.TryGetValue(characterType, out var jobName)) return null;
        return new SystemNotice(prefix, Fill(template, playerName, jobName));
    }

    public SystemNotice? Awakened(byte guardianSlot, string playerName)
    {
        ArgumentNullException.ThrowIfNull(playerName);
        if (guardianSlot != AnnouncedGuardianSlot) return null;
        if (!_changeJob.TryGetValue(PrefixRow, out var prefix) || !_awaken.TryGetValue(AwakenRow, out var template)) return null;
        return new SystemNotice(prefix, Fill(template, playerName));
    }

    public static string Fill(string template, params string[] values)
    {
        ArgumentNullException.ThrowIfNull(template);
        var text = new StringBuilder(template.Length + 16);
        var next = 0;
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] == '%' && i + 1 < template.Length && template[i + 1] == 's' && next < values.Length)
            {
                text.Append(values[next++]);
                i++;
                continue;
            }
            text.Append(template[i]);
        }
        return text.ToString();
    }
}
