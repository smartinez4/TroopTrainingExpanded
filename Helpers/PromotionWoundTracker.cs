using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

public static class PromotionWoundTracker
{
    class Expectation
    {
        public TroopRoster Roster;
        public CharacterObject UpgradeTarget;
        public CharacterObject SourceTroop;
        public int ExpectedXp;
    }

    static readonly List<Expectation> _expectations = new List<Expectation>();
    static readonly object _lock = new object();
    internal static readonly ThreadLocal<bool> InWoundPostfix = new ThreadLocal<bool>(() => false);

    public static void ExpectPromotionWound(TroopRoster roster, CharacterObject upgradeTarget, CharacterObject sourceTroop, int expectedXp)
    {
        if (roster == null || upgradeTarget == null || sourceTroop == null) return;
        var e = new Expectation { Roster = roster, UpgradeTarget = upgradeTarget, SourceTroop = sourceTroop, ExpectedXp = expectedXp };
        lock (_lock) { _expectations.Add(e); }
    }

    public static bool IsExpectation(TroopRoster roster, CharacterObject upgradeTarget)
    {
        lock (_lock) { return _expectations.Any(x => x.Roster == roster && x.UpgradeTarget == upgradeTarget); }
    }

    public static (CharacterObject sourceTroop, int expectedXp) PopExpectation(TroopRoster roster, CharacterObject upgradeTarget)
    {
        lock (_lock)
        {
            int idx = _expectations.FindIndex(x => x.Roster == roster && x.UpgradeTarget == upgradeTarget);
            if (idx < 0) return (null, 0);
            var e = _expectations[idx];
            _expectations.RemoveAt(idx);
            return (e.SourceTroop, e.ExpectedXp);
        }
    }

    public static void ClearAllExpectations()
    {
        lock (_lock) { _expectations.Clear(); }
    }
}

