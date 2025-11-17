using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingExpanded
{
    public class TrainingDuelBehavior : CampaignBehaviorBase
    {
        public static bool DuelInProgress = false;

        private bool _injectOnStart = false;

        // Troops selected for the duel
        private List<CharacterObject> _selectedTroops = new();
        private List<CharacterObject> _pendingTroops = new();

        private bool _duelQueued = false;

        private static bool _menusRegistered = false;
        private bool _eventsRegistered = false;

        // Promotions from duel
        private MultiDuelBehavior _activeDuel;

        public override void RegisterEvents()
        {
            if (_eventsRegistered) return;
            _eventsRegistered = true;

            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, OnMissionStarted);
            CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
            CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnGameMenuOpened);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            if (_menusRegistered) return;
            _menusRegistered = true;

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
            TroopRoster leftRoster = TroopRoster.CreateDummyTroopRoster();
            TroopRoster leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            TextObject leftName = new TextObject("Select Troops for Training Duel");

            const int leftLimit = 5; // max troops

            IsTroopTransferableDelegate transferable =
                (character, type, side, owner) => true;

            // DONE button validation (always enabled, logic handled in onDone)
            PartyPresentationDoneButtonConditionDelegate doneCondition =
                (lm, lp, rm, rp, limitL, limitR) =>
                    new System.Tuple<bool, TextObject>(true, TextObject.Empty);

            PartyPresentationDoneButtonDelegate onDoneClicked =
                (lm, lp, rm, rp, taken, released, forced, left, right) =>
                {
                    var selected = lm.GetTroopRoster()
                        .Where(e => e.Number > 0)
                        .ToList();

                    int selectedCount = selected.Sum(e => e.Number);

                    if (selectedCount < 1)
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage("You must select at least one troop.")
                        );
                        return false;
                    }

                    if (selectedCount > leftLimit)
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage($"You cannot select more than {leftLimit} troops.")
                        );
                        return false;
                    }

                    _pendingTroops.Clear();

                    foreach (var e in selected)
                    {
                        for (int i = 0; i < e.Number; i++)
                        {
                            _pendingTroops.Add(e.Character);

                            // Restore unit to party (UI temporarily removes it)
                            MobileParty.MainParty.MemberRoster.AddToCounts(e.Character, 1);
                        }
                    }

                    _duelQueued = true;
                    return true;
                };

            PartyScreenManager.OpenScreenWithCondition(
                transferable,
                doneCondition,
                onDoneClicked,
                null,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                leftName,
                leftLimit,
                false,
                false,
                PartyScreenMode.Normal,
                leftRoster,
                leftPrisoners
            );
        }

        // ---------------------------------------------------------------------
        //  Wait for return to "town" menu
        // ---------------------------------------------------------------------
        private void OnGameMenuOpened(MenuCallbackArgs args)
        {
            if (!_duelQueued)
                return;

            if (args.MenuContext.GameMenu.StringId != "town_arena")
                return;

            _duelQueued = false;

            _selectedTroops = _pendingTroops.ToList();
            _pendingTroops.Clear();

            StartDuel();
        }

        // ---------------------------------------------------------------------
        //  Start the duel mission
        // ---------------------------------------------------------------------
        private void StartDuel()
        {
            DuelInProgress = true;
            _injectOnStart = true;

            var settlement = Settlement.CurrentSettlement;
            var arena = settlement.LocationComplex.GetLocationWithId("arena");

            CampaignMission.OpenArenaStartMission(
                arena.GetSceneName(settlement.Town.GetWallLevel()),
                arena,
                null
            );
        }

        // ---------------------------------------------------------------------
        //  Inject mission logic (MultiDuelBehavior)
        // ---------------------------------------------------------------------
        private void OnMissionStarted(IMission iMission)
        {
            if (!_injectOnStart)
                return;

            if (iMission is Mission mission)
            {
                _activeDuel = new MultiDuelBehavior(_selectedTroops);
                mission.AddMissionBehavior(_activeDuel);
            }
            else
            {
                InformationManager.DisplayMessage(
                    new InformationMessage("Mission cast failed.")
                );
            }

            _injectOnStart = false;
        }

        // ---------------------------------------------------------------------
        //  After mission ends → Apply promotions
        // ---------------------------------------------------------------------
        private void OnMissionEnded(IMission mission)
        {
            DuelInProgress = false;

            if (mission is Mission m)
            {
                var duel = m.GetMissionBehavior<MultiDuelBehavior>();
                if (duel != null)
                {
                    ApplyPromotionXp(duel.DefeatedTroops);
                }
            }
        }

        private void ApplyPromotionXp(IEnumerable<CharacterObject> defeated)
        {
            if (defeated == null)
                return;

            var model = Campaign.Current.Models.PartyTroopUpgradeModel;

            foreach (var troop in defeated)
            {
                int added = PromotionHelpers.GrantXpForPromotion(model, troop);

                if (added > 0)
                {
                    InformationManager.DisplayMessage(
                        new InformationMessage($"{troop.Name} is now ready for promotion!")
                    );
                }
            }
        }

    }
}
