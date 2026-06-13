﻿using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
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

namespace TroopTrainingExpanded.Behaviors
{
    public class TrainingCampaignBehavior : CampaignBehaviorBase
    {
        public static bool DuelInProgress;

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
            var menuOptionName = new TextObject("{=ttx_training_fight}Training fight").ToString();

            starter.AddGameMenuOption("town_arena", "ttx_arena_menu", menuOptionName,
                args => {
                    args.optionLeaveType = GameMenuOption.LeaveType.PracticeFight;
                    if (Hero.MainHero.IsWounded)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=ttx_hero_wounded}You cannot participate in training fights while wounded.");
                    }
                    if (Campaign.Current.TournamentManager.GetTournamentGame(Settlement.CurrentSettlement.Town) != null)
                    {
                        args.IsEnabled = false;
                        args.Tooltip = new TextObject("{=ttx_tournament_on}You cannot participate in training fights while a tournament is in progress.");
                    }
                    return true;
                },
                _ => OpenTroopSelectionScreen(),
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
            var partyRoles = SnapshotPartyRoles();

            var leftRoster = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();

            static bool Transferable(CharacterObject c, PartyScreenLogic.TroopType t,
                PartyScreenLogic.PartyRosterSide s, PartyBase o) => true;

            static Tuple<bool, TextObject> AlwaysOk(TroopRoster lm, TroopRoster lp, TroopRoster rm,
                TroopRoster rp, int lL, int rL) =>
                new(true, TextObject.GetEmpty());

            PartyScreenHelper.OpenScreenWithCondition(
                Transferable,
                AlwaysOk,
                OnDone,
                null,
                PartyScreenLogic.TransferState.Transferable,
                PartyScreenLogic.TransferState.NotTransferable,
                new TextObject("{=ttx_select_troops}Select troops to fight"),
                maxTroopSelection,
                false,
                false,
                PartyScreenHelper.PartyScreenMode.Normal,
                leftRoster,
                leftPrisoners
            );
            return;

            bool OnDone(TroopRoster lm, TroopRoster troopRoster, TroopRoster troopRoster1, TroopRoster troopRoster2, FlattenedTroopRoster flattenedTroopRoster, FlattenedTroopRoster flattenedTroopRoster1, bool b, PartyBase partyBase, PartyBase partyBase1)
            {
                var roster = lm.GetTroopRoster().Where(e => e.Number > 0).ToList();
                int count = roster.Sum(e => e.Number);

                if (count < 1)
                {
                    var msg = new TextObject("{=ttx_select_one}Select at least one unit.").ToString();
                    InformationManager.DisplayMessage(new InformationMessage(msg));
                    return false;
                }

                if (count > maxTroopSelection)
                {
                    var msg = new TextObject("{=ttx_select_max}You cannot select more than {maxTroopSelection} troops.");
                    msg.SetTextVariable("maxTroopSelection", maxTroopSelection);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString()));
                    return false;
                }

                _selectedTroops.Clear();
                foreach (var unit in roster)
                {
                    for (var i = 0; i < unit.Number; i++)
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

                RestoreCompanionPartyRoles(partyRoles);
                _awaitingMissionStart = true;
                return true;
            }
        }

        private static Dictionary<Hero, List<PartyRole>> SnapshotPartyRoles()
        {
            var party = MobileParty.MainParty;
            return party.MemberRoster.GetTroopRoster()
                .Where(element => element.Character.IsHero)
                .Select(element => element.Character.HeroObject)
                .Where(hero => hero != null)
                .ToDictionary(hero => hero, hero => party.GetHeroPartyRoles(hero).ToList());
        }

        private void RestoreCompanionPartyRoles(IReadOnlyDictionary<Hero, List<PartyRole>> partyRoles)
        {
            var party = MobileParty.MainParty;

            foreach (var companion in _companionTroops.Select(character => character.HeroObject).Distinct())
            {
                if (companion == null || !partyRoles.TryGetValue(companion, out var roles))
                    continue;

                foreach (var role in roles)
                    party.SetHeroPartyRole(companion, role);
            }
        }

        private void OnGameMenuOpened(MenuCallbackArgs args)
        {
            if (!_awaitingMissionStart ||
                args.MenuContext.GameMenu.StringId != "town_arena") return;
            _awaitingMissionStart = false;
            StartDuel();
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

            if (iMission is not Mission mission) return;
            _activeDuel = new ArenaTrainingCombatBehavior(_troops, _companionTroops);
            mission.AddMissionBehavior(_activeDuel);
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
                var isAdded = PromotionHelpers.GrantXpForPromotion(model, troop);
                if (!isAdded) continue;
                var msg = new TextObject("{=ttx_ready_promotion}{unit} is ready for promotion!");
                msg.SetTextVariable("unit", troop.Name);
                InformationManager.DisplayMessage(
                    new InformationMessage(msg.ToString())
                );
            }
        }
    }
}
