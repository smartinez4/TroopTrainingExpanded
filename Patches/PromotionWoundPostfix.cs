using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

[HarmonyPatch]
public static class PromotionWoundPostfix
{
    private static readonly MethodBase _target =
        AccessTools.Method(typeof(TroopRoster), "AddToCounts",
            new[]
            {
                typeof(CharacterObject),
                typeof(int),
                typeof(bool),
                typeof(int),
                typeof(int),
                typeof(bool),
                typeof(int)
            });

    static MethodBase TargetMethod() => _target;

    static void Postfix(
        TroopRoster __instance,
        CharacterObject character,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChange,
        bool removeDepleted,
        int index,
        int __result)
    {
        if (__result < 0 ||
            PromotionWoundTracker.InWoundPostfix.Value ||
            !PromotionWoundTracker.IsExpectation(__instance, character))
            return;

        PromotionWoundTracker.InWoundPostfix.Value = true;

        try
        {
            var (source, xp) = PromotionWoundTracker.PopExpectation(__instance, character);
            if (source == null) return;

            Safe(() => __instance.AddToCountsAtIndex(__result, 0, 1));

            RemoveMatching(__instance, source, xp);
            RemoveStray(__instance, source, xp);
        }
        catch
        {
            PromotionWoundTracker.ClearAllExpectations();
        }
        finally
        {
            PromotionWoundTracker.InWoundPostfix.Value = false;
        }
    }

    private static void RemoveMatching(TroopRoster r, CharacterObject c, int xp)
    {
        for (int i = 0, n = r.Count; i < n; i++)
        {
            if (r.GetCharacterAtIndex(i) == c &&
                r.GetElementWoundedNumber(i) > 0 &&
                r.GetElementXp(i) == xp)
            {
                Safe(() => r.AddToCountsAtIndex(i, 0, -1));
                return;
            }
        }
    }

    private static void RemoveStray(TroopRoster r, CharacterObject c, int xp)
    {
        for (int i = 0, n = r.Count; i < n; i++)
        {
            if (r.GetCharacterAtIndex(i) == c &&
                r.GetElementXp(i) != xp &&
                r.GetElementWoundedNumber(i) > 0)
            {
                Safe(() => r.AddToCountsAtIndex(i, 0, -1));
                return;
            }
        }
    }

    private static void Safe(Action a)
    {
        try { a(); } catch { }
    }
}
