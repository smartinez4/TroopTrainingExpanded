using SandBox.Missions.MissionLogics;
using SandBox.Missions.MissionLogics.Arena;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TroopTrainingExpanded
{
    public class ArenaTrainingCombatBehavior(List<CharacterObject> troops, List<CharacterObject> companions) : MissionLogic
    {
        public IReadOnlyList<CharacterObject> DefeatedTroops => _defeatedTroops;

        private readonly List<CharacterObject> _defeatedTroops = new();
        private readonly List<CharacterObject> _troops = troops ?? new List<CharacterObject>();
        private readonly List<CharacterObject> _companions = companions ?? new List<CharacterObject>();

        private Vec3 _playerSpawnPos;
        private Vec3 _playerForward;
        private readonly List<Vec3> _enemySpawnPositions = new();
        private bool _playerSpawned = false;

        private float _deathTimer = -1f;
        private const float DeathDelay = 3f;

        private bool _victoryShown = false;

        public override void AfterStart()
        {
            _defeatedTroops.Clear();
            ResolveSpawnPositions();
            SpawnAll();
        }

        private void ResolveSpawnPositions()
        {
            var playerEntity = Mission.Scene.FindEntityWithTag("sp_arena_player");
            var opponentEntities = Mission.Scene.FindEntitiesWithTag("sp_arena_opponent").ToList();
            var genericEntities = Mission.Scene.FindEntitiesWithTag("sp_arena").ToList();

            ResolvePlayerSpawn(playerEntity, genericEntities);
            ResolveEnemySpawns(opponentEntities, genericEntities);

            // Minimal safety fallback
            if (_enemySpawnPositions.Count == 0)
                _enemySpawnPositions.Add(_playerSpawnPos + new Vec3(2f, 0f, 0f));
        }

        private void ResolvePlayerSpawn(GameEntity playerEntity, List<GameEntity> generic)
        {
            if (playerEntity != null)
            {
                var f = CleanFrame(playerEntity.GetGlobalFrame());
                _playerSpawnPos = f.origin;
                _playerForward = f.rotation.f;
                return;
            }

            if (generic.Count > 0)
            {
                var f = CleanFrame(generic.First().GetGlobalFrame());
                _playerSpawnPos = f.origin;
                _playerForward = f.rotation.f;
                return;
            }

            // Fallback: use current player position
            if (Mission.MainAgent != null)
            {
                _playerSpawnPos = Mission.MainAgent.Position;
                _playerForward = new Vec3(0f, 1f, 0f);
            }
            else
            {
                _playerSpawnPos = Vec3.Zero;
                _playerForward = new Vec3(0f, 1f, 0f);
            }
        }

        private void ResolveEnemySpawns(List<GameEntity> opponents, List<GameEntity> generic)
        {
            if (opponents.Count > 0)
            {
                _enemySpawnPositions.AddRange(
                    opponents.Select(e => CleanFrame(e.GetGlobalFrame()).origin)
                );
                return;
            }

            if (generic.Count > 0)
            {
                var positions = generic
                    .Select(e => CleanFrame(e.GetGlobalFrame()).origin)
                    .Where(p => !p.NearlyEquals(_playerSpawnPos, 0.1f))
                    .ToList();

                if (positions.Count > 0)
                {
                    _enemySpawnPositions.AddRange(positions);
                    return;
                }
            }

            int needed = Math.Max(1, _troops.Count);
            const float radius = 2f;

            for (int i = 0; i < needed; i++)
            {
                float angle = (float)(i * (2 * Math.PI / needed));
                var offset = new Vec3(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius, 0f);
                _enemySpawnPositions.Add(_playerSpawnPos + offset);
            }
        }

        private MatrixFrame CleanFrame(MatrixFrame f)
        {
            f.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
            var o = f.origin;
            o.z = Mission.Scene.GetGroundHeightAtPosition(o);
            f.origin = o;
            return f;
        }

        private void SpawnAll()
        {
            if (Mission.MainAgent == null)
                return;

            Team attackers = Mission.AttackerTeam;
            attackers.SetIsEnemyOf(Mission.PlayerTeam, true);
            Mission.PlayerTeam.SetIsEnemyOf(attackers, true);

            SpawnPlayer();
            SpawnCompanions();
            SpawnEnemies(attackers);
        }

        private void SpawnPlayer()
        {
            if (Mission.MainAgent != null && Mission.MainAgent.IsActive())
                Mission.MainAgent.FadeOut(true, true);

            Hero hero = Hero.MainHero;
            CharacterObject character = hero.CharacterObject;

            AgentBuildData data = new AgentBuildData(character)
                .Team(Mission.PlayerTeam)
                .Controller(AgentControllerType.Player)
                .Equipment(hero.BattleEquipment)
                .NoHorses(!EquipHeroHorse())
                .TroopOrigin(new PartyAgentOrigin(Hero.MainHero.PartyBelongedTo.Party, character))
                .InitialPosition(_playerSpawnPos)
                .InitialDirection(new Vec2(_playerForward.x, _playerForward.y));

            Agent agent = Mission.SpawnAgent(data);
            Mission.MainAgent = agent;

            agent.LookDirection = _playerForward;
        }

        private void SpawnCompanions()
        {
            foreach (var comp in _companions)
            {
                Vec3 offset = _playerSpawnPos + new Vec3(MBRandom.RandomFloatRanged(-1f, 1f), MBRandom.RandomFloatRanged(-1f, 1f), 0);
                Vec3 lookDir = _playerForward;

                Agent agent = Mission.SpawnAgent(
                    new AgentBuildData(comp)
                        .Team(Mission.PlayerTeam)
                        .InitialPosition(offset)
                        .InitialDirection(new Vec2(lookDir.x, lookDir.y))
                        .TroopOrigin(new PartyAgentOrigin(comp.HeroObject.PartyBelongedTo.Party, comp))
                        .NoHorses(!EquipHeroHorse())
                        .CivilianEquipment(false)
                );

                agent.Controller = AgentControllerType.AI;
                agent.SetWatchState(Agent.WatchState.Alarmed);
                agent.SetMorale(100f);
                agent.LookDirection = lookDir;
            }
        }

        private void SpawnEnemies(Team enemyTeam)
        {
            for (int i = 0; i < _troops.Count; i++)
            {
                Vec3 pos = _enemySpawnPositions[i % _enemySpawnPositions.Count];
                Vec3 lookDir = ComputeLookDirection(pos, _playerSpawnPos);
                SpawnEnemy(_troops[i], pos, lookDir, enemyTeam);
            }
        }

        private static Vec3 ComputeLookDirection(Vec3 from, Vec3 to)
        {
            Vec3 d = to - from;
            float len = d.Length;
            return len > 1e-5f ? d / len : new Vec3(0f, 1f, 0f);
        }

        private void SpawnEnemy(CharacterObject troop, Vec3 pos, Vec3 lookDir, Team enemyTeam)
        {
            Vec2 look2 = new Vec2(lookDir.x, lookDir.y);

            Agent agent = Mission.SpawnAgent(
                new AgentBuildData(troop)
                    .Team(enemyTeam)
                    .InitialPosition(pos)
                    .InitialDirection(look2)
                    .NoHorses(!HasHorse(troop))
                    .TroopOrigin(new SimpleAgentOrigin(troop))
                    .CivilianEquipment(false)
            );

            agent.Controller = AgentControllerType.AI;
            agent.SetWatchState(Agent.WatchState.Alarmed);
            agent.SetMorale(100f);
            agent.LookDirection = lookDir;
        }
        private bool EquipHeroHorse()
        {
            bool heroHasHorse = HasHorse(Hero.MainHero.CharacterObject);

            if (!heroHasHorse)
                return false;

            foreach (var troop in _troops)
            {
                if (HasHorse(troop))
                    return true;
            }

            return false;
        }

        private static bool HasHorse(CharacterObject character)
        {
            var horse = character?.Equipment?.GetEquipmentFromSlot(EquipmentIndex.Horse).Item;
            return horse != null;
        }

        public override void OnAgentRemoved(Agent affected, Agent affector, AgentState state, KillingBlow blow)
        {
            if (affected?.Character is not CharacterObject c)
                return;

            if (affector != null && affector.Team == Mission.PlayerTeam)
            {
                _defeatedTroops.Add(c);
            }
        }

        public override void OnMissionTick(float dt)
        {
            if (!_playerSpawned)
            {
                if (Mission.MainAgent != null && Mission.MainAgent.IsActive())
                    _playerSpawned = true;

                return;
            }

            var agent = Mission.MainAgent;

            // Player dead
            if (agent == null || agent.State == AgentState.Killed || agent.Health <= 0f)
            {
                if (_deathTimer < 0f)
                    _deathTimer = 0f;

                _deathTimer += dt;

                if (_deathTimer >= DeathDelay)
                {
                    GameMenu.SwitchToMenu("town");
                    Mission.EndMission();
                }
                return;
            }

            // Victory
            if (_victoryShown)
                return;

            bool anyEnemiesAlive = Mission.Agents.Any(a =>
                a != null &&
                a.Team == Mission.AttackerTeam &&
                a != Mission.MainAgent &&
                a.IsActive());

            if (!anyEnemiesAlive)
            {
                MBInformationManager.AddQuickInformation(
                    new TextObject("{=ttx_victory_leave}Victory! Hold Tab to leave"), 3000
                );
                _victoryShown = true;
            }
        }
    }
}
