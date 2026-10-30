namespace tPoseidon.Core.InterServer;

public static class BrokerAckFamily
{
    public static bool TryResolveOkReply(InterServerOpcode request, out InterServerOpcode okReply)
    {
        switch (request)
        {
            case InterServerOpcode.IcmdPickup:
                okReply = InterServerOpcode.IcmdPickupAckOk;
                return true;
            case InterServerOpcode.IcmdDrop:
                okReply = InterServerOpcode.IcmdDropAckOk;
                return true;
            case InterServerOpcode.QcmdAcceptQuest:
                okReply = InterServerOpcode.QcmdAcceptQuestAckOk;
                return true;
            case InterServerOpcode.QcmdCompleteQuest:
                okReply = InterServerOpcode.QcmdCompleteQuestAckOk;
                return true;
            default:
                okReply = default;
                return false;
        }
    }
}
