using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;

public static class PromotionHelpers
{
    public static int GrantXpForPromotion(
        PartyTroopUpgradeModel model,
        CharacterObject troop)
    {
        if (model == null || troop == null)
            return 0;

        var party = PartyBase.MainParty;
        if (party == null)
            return 0;

        // Determine upgrade target
        CharacterObject upgradeTarget =
            troop.UpgradeTargets != null && troop.UpgradeTargets.Length > 0
                ? troop.UpgradeTargets[0]
                : null;

        if (upgradeTarget == null)
            return 0;

        // Required XP for upgrade
        int requiredXp =
            model.GetXpCostForUpgrade(party, troop, upgradeTarget);

        if (requiredXp <= 0)
            return 0;

        // Give the full XP
        int added = party.MemberRoster.AddXpToTroop(requiredXp, troop);

        return added;
    }
}
