using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TroopTrainingExpanded.Helpers;

namespace TroopTrainingExpanded
{
    public class TrainingCampaignBehavior : CampaignBehaviorBase
    {
        public static bool DuelInProgress = false;

        private readonly List<CharacterObject> _selectedTroops = [];
        private readonly List<CharacterObject> _companionTroops = [];
        private readonly List<CharacterObject> _troops = [];

        private bool _awaitingMissionStart;
        private ArenaTrainingCombatBehavior _activeDuel;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, OnMissionStarted);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                "town_arena",
                "ttx_arena_menu",
                "Training fight",
                args => { args.IsEnabled = true; return true; },
                args => OpenTroopSelectionScreen(),
                false,
                3
            );
        }

        private void OpenTroopSelectionScreen()
        {
            _selectedTroops.Clear();
            _companionTroops.Clear();
            _troops.Clear();

            int maxTroopSelection = ModConfig.Instance.MaxTrainingTroops;

            var leftRoster = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();

            static bool Transferable(CharacterObject c, PartyScreenLogic.TroopType t,
                PartyScreenLogic.PartyRosterSide s, PartyBase o) => true;

            static Tuple<bool, TextObject> AlwaysOk(TroopRoster lm, TroopRoster lp, TroopRoster rm,
                TroopRoster rp, int lL, int rL) =>
                new(true, TextObject.GetEmpty());

            PartyPresentationDoneButtonDelegate onDone = (lm, lp, rm, rp, taken, rel, forced, leftParty, rightParty) =>
            {
                var roster = lm.GetTroopRoster().Where(e => e.Number > 0).ToList();
                int count = roster.Sum(e => e.Number);

                if (count < 1)
                {
                    InformationManager.DisplayMessage(new InformationMessage("Select at least one troop."));
                    return false;
                }

                if (count > maxTroopSelection)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage($"You cannot select more than {maxTroopSelection} troops."));
                    return false;
                }

                bool hasWounded = roster.Any(e => e.WoundedNumber > 0);
                if (hasWounded)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage("You cannot send wounded troops into training fights."));
                    return false;
                }

                _selectedTroops.Clear();
                foreach (var unit in roster)
                {
                    for (int i = 0; i < unit.Number; i++)
                    {
                        _selectedTroops.Add(unit.Character);
                        if (unit.Character.IsHero && unit.Character.HeroObject != Hero.MainHero)
                        {
                            _companionTroops.Add(unit.Character);
                        }
                        else
                        {
                             _troops.Add(unit.Character);
                        }
                        MobileParty.MainParty.MemberRoster.AddToCounts(unit.Character, 1);
                    }
                }

                _awaitingMissionStart = true;
                return true;
            };

            PartyScreenHelper.OpenScreenWithCondition(
                Transferable,
                AlwaysOk,
                onDone,
                null,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                new TextObject("Select troops to fight"),
                maxTroopSelection,
                false,
                false,
                PartyScreenHelper.PartyScreenMode.Normal,
                leftRoster,
                leftPrisoners
            );
        }

        private void OnGameMenuOpened(MenuCallbackArgs args)
        {
            if (_awaitingMissionStart &&
                args.MenuContext.GameMenu.StringId == "town_arena")
            {
                _awaitingMissionStart = false;
                StartDuel();
            }
        }

        private void StartDuel()
        {
            DuelInProgress = true;

            var settlement = Settlement.CurrentSettlement;
            var arena = settlement.LocationComplex.GetLocationWithId("arena");

            _awaitingMissionStart = true;

            CampaignMission.OpenArenaStartMission(
                arena.GetSceneName(settlement.Town.GetWallLevel()),
                arena,
                null
            );
        }

        private void OnMissionStarted(IMission iMission)
        {
            if (!_awaitingMissionStart)
                return;

            _awaitingMissionStart = false;

            if (iMission is Mission mission)
            {
                _activeDuel = new ArenaTrainingCombatBehavior(_troops, _companionTroops);
                mission.AddMissionBehavior(_activeDuel);
            }
        }

        private void OnMissionEnded(IMission mission)
        {
            DuelInProgress = false;

            _selectedTroops.Clear();
            _companionTroops.Clear();
            _troops.Clear();

            if (mission is not Mission m) return;

            var duel = m.GetMissionBehavior<ArenaTrainingCombatBehavior>();
            if (duel != null)
                ApplyPromotionXp(duel.DefeatedTroops);
        }

        private void ApplyPromotionXp(IEnumerable<CharacterObject> defeated)
        {
            if (defeated == null) return;

            var model = Campaign.Current.Models.PartyTroopUpgradeModel;

            foreach (var troop in defeated)
            {
                bool isAdded = PromotionHelpers.GrantXpForPromotion(model, troop);
                if (isAdded)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage($"{troop.Name} is ready for promotion!")
                    );
                }
            }
        }
    }
}
