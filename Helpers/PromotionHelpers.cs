using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

public static class PromotionHelpers
{
    public static bool GrantXpForPromotion(PartyTroopUpgradeModel model, CharacterObject troop)
    {
        if (model == null || troop == null) return false;
        var party = PartyBase.MainParty;
        if (party == null) return false;

        var roster = party.MemberRoster;

        int firstIndex = -1;
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster.GetCharacterAtIndex(i) == troop)
            {
                firstIndex = i;
                break;
            }
        }
        if (firstIndex < 0) return false;

        if (!TryRemoveOneHealthy(roster, troop))
            return false;

        var upgradeTarget = troop.UpgradeTargets?.Length > 0 ? troop.UpgradeTargets[0] : null;
        if (upgradeTarget == null) return false;

        int xp = model.GetXpCostForUpgrade(party, troop, upgradeTarget);
        if (xp <= 0) return false;

        int idx = roster.AddToCounts(
            troop,
            1,
            false,
            1,      // wounded
            xp,     // carry this XP to match later
            true,
            firstIndex
        );

        if (idx < 0) return false;

        PromotionWoundTracker.ExpectPromotionWound(roster, upgradeTarget, troop, xp);
        return true;
    }

    private static bool TryRemoveOneHealthy(TroopRoster roster, CharacterObject troop)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster.GetCharacterAtIndex(i) != troop)
                continue;

            int total = roster.GetElementNumber(i);
            int wounded = roster.GetElementWoundedNumber(i);
            if (total - wounded > 0)
            {
                roster.AddToCountsAtIndex(i, -1, 0);
                return true;
            }
        }
        return false;
    }
}
