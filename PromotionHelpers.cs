using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

public static class PromotionHelpers
{
    public static bool GrantXpForPromotion(
        PartyTroopUpgradeModel model,
        CharacterObject troop)
    {
        if (model == null || troop == null)
            return false;

        var party = PartyBase.MainParty;
        if (party == null)
            return false;

        CharacterObject upgradeTarget =
            troop.UpgradeTargets != null && troop.UpgradeTargets.Length > 0
                ? troop.UpgradeTargets[0]
                : null;

        if (upgradeTarget == null)
            return false;

        int requiredXp =
            model.GetXpCostForUpgrade(party, troop, upgradeTarget);

        if (requiredXp <= 0)
            return false;

        party.MemberRoster.AddXpToTroop(troop, requiredXp);

        return true;
    }
}
