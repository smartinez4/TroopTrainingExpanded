using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

public static class PromotionHelpers
{
    public static bool GrantXpForPromotion(PartyTroopUpgradeModel model, CharacterObject troop)
    {
        if (model == null || troop == null) return false;

        var party = PartyBase.MainParty;
        if (party == null) return false;

        var roster = party.MemberRoster;

        for (int i = 0; i < roster.Count; i++)
        {
            if (roster.GetCharacterAtIndex(i) != troop)
                continue;

            var upgradeTarget = troop.UpgradeTargets?.FirstOrDefault();
            if (upgradeTarget == null) 
                return false;

            int xp = model.GetXpCostForUpgrade(party, troop, upgradeTarget);
            if (xp <= 0)
                return false;

            roster.AddXpToTroop(troop, xp);
            return true;
        }

        return false;
    }
}
