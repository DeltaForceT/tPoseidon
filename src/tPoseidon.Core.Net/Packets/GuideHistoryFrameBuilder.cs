using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Packets;

public static class GuideHistoryFrameBuilder
{
    public const ulong TutorialGuideWelcomeSeenBit = 1UL << 3;

    public static byte[] Build(
        ulong characterId,
        IGuideHistoryStore? store,
        IQuestStore? questStore = null,
        IQuestHistoryStore? questHistoryStore = null)
    {
        var buf = new byte[264];

        var row = store?.GetForCharacter(characterId);
        if (row is not null && !string.IsNullOrEmpty(row.SystemGuide))
        {
            var bytes = System.Text.Encoding.ASCII.GetBytes(row.SystemGuide);
            var copy = Math.Min(bytes.Length, 256);
            Array.Copy(bytes, 0, buf, 0, copy);
        }

        var tutorialGuide = row?.TutorialGuide ?? 0UL;
        if (HasAnyQuestActivity(characterId, questStore, questHistoryStore))
        {
            tutorialGuide |= TutorialGuideWelcomeSeenBit;
        }

        var t = tutorialGuide;
        for (int i = 0; i < 8; i++)
        {
            buf[256 + i] = (byte)(t & 0xFF);
            t >>= 8;
        }
        return buf;
    }

    private static bool HasAnyQuestActivity(
        ulong characterId,
        IQuestStore? questStore,
        IQuestHistoryStore? questHistoryStore)
    {
        if (questStore is not null && questStore.GetActiveForCharacter(characterId).Count > 0)
            return true;
        if (questHistoryStore is not null && questHistoryStore.GetForCharacter(characterId).Count > 0)
            return true;
        return false;
    }
}
