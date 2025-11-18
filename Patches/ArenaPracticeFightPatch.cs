using HarmonyLib;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingExpanded
{
    [HarmonyPatch(typeof(ArenaPracticeFightMissionController))]
    public static class ArenaPracticeFightPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("StartPractice")]
        static bool StopPracticeFight()
        {
            return !TrainingCampaignBehavior.DuelInProgress;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnMissionTick")]
        static bool StopSpawning()
        {
            return !TrainingCampaignBehavior.DuelInProgress;
        }

        [HarmonyPostfix]
        [HarmonyPatch("AfterStart")]
        static void FixPlayerSpawn(ArenaPracticeFightMissionController __instance)
        {
            if (!TrainingCampaignBehavior.DuelInProgress)
                return;

            var mission = Mission.Current;
            if (mission?.MainAgent == null) return;

            mission.MainAgent.TeleportToPosition(mission.Scene.FindEntityWithTag("sp_arena").GetGlobalFrame().origin);
        }
    }
}
