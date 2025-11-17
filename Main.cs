using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingExpanded
{
    public class Main : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Initialize Harmony and apply all patches in this assembly
            var harmony = new Harmony("trooptrainingexpanded.duel");
            harmony.PatchAll();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (gameStarterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new TrainingDuelBehavior());
            }
        }
    }
}
