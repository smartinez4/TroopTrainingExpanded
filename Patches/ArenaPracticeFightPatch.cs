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
            return !TrainingDuelBehavior.DuelInProgress;
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnMissionTick")]
        static bool StopSpawning()
        {
            return !TrainingDuelBehavior.DuelInProgress;
        }

        [HarmonyPostfix]
        [HarmonyPatch("AfterStart")]
        static void FixPlayerSpawn(ArenaPracticeFightMissionController __instance)
        {
            if (!TrainingDuelBehavior.DuelInProgress)
                return;

            var mission = Mission.Current;
            if (mission?.MainAgent == null) return;

            mission.MainAgent.TeleportToPosition(mission.Scene.FindEntityWithTag("sp_arena").GetGlobalFrame().origin);
        }
    }
}
