using HarmonyLib;
using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.MountAndBlade;
using TroopTrainingExpanded.Behaviors;

namespace TroopTrainingExpanded.Patches
{
    [HarmonyPatch(typeof(ArenaPracticeFightMissionController))]
    public static class ArenaPracticeFightPatch
    {
        internal static bool IsTrainingMission(Mission mission)
        {
            return mission?.GetMissionBehavior<ArenaTrainingCombatBehavior>() != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch("StartPractice")]
        static bool StopPracticeFight(ArenaPracticeFightMissionController __instance)
        {
            return !IsTrainingMission(__instance.Mission);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnMissionTick")]
        static bool StopSpawning(ArenaPracticeFightMissionController __instance)
        {
            return !IsTrainingMission(__instance.Mission);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnAgentRemoved")]
        static bool StopPracticeScoring(ArenaPracticeFightMissionController __instance)
        {
            return !IsTrainingMission(__instance.Mission);
        }
    }

    // Arena startup spawns location NPCs separately from StartPractice.
    [HarmonyPatch(typeof(MissionAgentHandler), "SpawnLocationCharacters")]
    public static class TrainingArenaLocationCharactersPatch
    {
        [HarmonyPrefix]
        static bool StopLocationSpawning(MissionAgentHandler __instance)
        {
            return !ArenaPracticeFightPatch.IsTrainingMission(__instance.Mission);
        }
    }

    [HarmonyPatch(typeof(DefaultCombatXpModel), nameof(DefaultCombatXpModel.GetXpFromHit))]
    public static class TrainingFightCombatXpPatch
    {
        [HarmonyPrefix]
        static void UseFieldBattleXp(ref CombatXpModel.MissionTypeEnum missionType)
        {
            if (TrainingCampaignBehavior.DuelInProgress)
                missionType = CombatXpModel.MissionTypeEnum.Battle;
        }
    }
}
