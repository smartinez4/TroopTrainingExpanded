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
    public class MultiDuelBehavior(List<CharacterObject> troops) : MissionLogic
    {
        public IReadOnlyList<CharacterObject> DefeatedTroops => _defeatedTroops;

        private readonly List<CharacterObject> _defeatedTroops = new();
        private readonly List<CharacterObject> _troops = troops ?? new List<CharacterObject>();
        private ArenaPracticeFightMissionController _arena;

        private Vec3 _playerSpawnPos;
        private Vec3 _playerForward;
        private readonly List<Vec3> _enemySpawnPositions = new();
        private bool _playerSpawned = false;
        private float _deathTimer = -1f;
        private const float DeathDelay = 3f;
        private bool _victoryShown = false;

        public override void AfterStart()
        {
            _arena = Mission.GetMissionBehavior<ArenaPracticeFightMissionController>();

            ResolveSpawnPositions();
            SpawnAll();
        }

        // =====================================================================
        //  1) RESOLVE PLAYER + ENEMY SPAWN POSITIONS
        // =====================================================================

        private void ResolveSpawnPositions()
        {
            // --- Player spawn marker ("sp_arena_player") ---
            var playerEntity = Mission.Scene.FindEntityWithTag("sp_arena_player");

            // --- Specific enemy markers ("sp_arena_opponent") ---
            var opponentEntities = Mission.Scene.FindEntitiesWithTag("sp_arena_opponent").ToList();

            // --- Generic arena markers ---
            var genericEntities = Mission.Scene.FindEntitiesWithTag("sp_arena").ToList();

            ResolvePlayerSpawn(playerEntity, genericEntities);
            ResolveEnemySpawns(opponentEntities, genericEntities);
        }

        private void ResolvePlayerSpawn(GameEntity playerEntity, List<GameEntity> generic)
        {
            if (playerEntity != null)
            {
                var f = playerEntity.GetGlobalFrame();
                NormalizeFrame(ref f);
                _playerSpawnPos = f.origin;
                _playerForward = f.rotation.f;
                return;
            }

            if (generic.Count > 0)
            {
                var f = generic.First().GetGlobalFrame();
                NormalizeFrame(ref f);
                _playerSpawnPos = f.origin;
                _playerForward = f.rotation.f;
                return;
            }

            // fallback → player stays where he is
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

        private void ResolveEnemySpawns(List<GameEntity> opponentEntities, List<GameEntity> generic)
        {
            if (opponentEntities.Count > 0)
            {
                _enemySpawnPositions.AddRange(
                    opponentEntities.Select(e => CleanFrame(e.GetGlobalFrame()))
                );
                return;
            }

            if (generic.Count > 0)
            {
                _enemySpawnPositions.AddRange(
                    generic
                        .Where(e => !e.GetGlobalFrame().origin.NearlyEquals(_playerSpawnPos, 0.1f))
                        .Select(e => CleanFrame(e.GetGlobalFrame()))
                );
                return;
            }

            // final fallback: semicircle
            int count = Math.Min(5, _troops.Count);
            _enemySpawnPositions.AddRange(
                ComputeFallbackSemicircle(_playerSpawnPos, _playerForward, count, 7f)
            );
        }

        private Vec3 CleanFrame(MatrixFrame frame)
        {
            NormalizeFrame(ref frame);
            var p = frame.origin;
            p.z = Mission.Scene.GetGroundHeightAtPosition(p);
            return p;
        }

        private void NormalizeFrame(ref MatrixFrame frame)
        {
            frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        }

        // =====================================================================
        //  2) SEMICIRCLE FALLBACK (only used if no markers exist)
        // =====================================================================

        private List<Vec3> ComputeFallbackSemicircle(Vec3 center, Vec3 forward, int count, float radius)
        {
            var list = new List<Vec3>();
            if (count <= 0) return list;

            float baseAngle = MathF.Atan2(forward.y, forward.x);
            float span = MathF.PI;
            float step = span / (count + 1);

            for (int i = 1; i <= count; i++)
            {
                float angle = baseAngle - span / 2f + step * i;

                var p = new Vec3(
                    center.x + radius * MathF.Cos(angle),
                    center.y + radius * MathF.Sin(angle),
                    0f
                );
                p.z = Mission.Scene.GetGroundHeightAtPosition(p);

                list.Add(p);
            }

            return list;
        }

        // =====================================================================
        //  3) SPAWN LOGIC (player + all enemies)
        // =====================================================================

        private void SpawnAll()
        {
            if (Mission.MainAgent == null)
                return;

            // Ensure hostility
            Team attackers = Mission.AttackerTeam;
            attackers.SetIsEnemyOf(Mission.PlayerTeam, true);
            Mission.PlayerTeam.SetIsEnemyOf(attackers, true);

            SpawnPlayer();
            SpawnEnemies(attackers);
        }

        private void SpawnPlayer()
        {
            if (Mission.MainAgent != null && Mission.MainAgent.IsActive())
            {
                Mission.MainAgent.FadeOut(true, true);
            }

            var handler = Mission.GetMissionBehavior<MissionAgentHandler>();
            handler?.SpawnPlayer(false, true); // no civilian gear, no horses

            if (Mission.MainAgent != null)
            {
                Mission.MainAgent.TeleportToPosition(_playerSpawnPos);
                Mission.MainAgent.LookDirection = _playerForward;
            }
        }


        private void SpawnEnemies(Team enemyTeam)
        {
            for (int i = 0; i < _troops.Count; i++)
            {
                var pos = _enemySpawnPositions[i % _enemySpawnPositions.Count];
                var lookDir = ComputeLookDirection(pos, _playerSpawnPos);

                SpawnEnemy(_troops[i], pos, lookDir, enemyTeam);
            }
        }

        private Vec3 ComputeLookDirection(Vec3 from, Vec3 to)
        {
            Vec3 dir = to - from;
            float len = dir.Length;

            return len > 1e-5f ? dir / len : new Vec3(0f, 1f, 0f);
        }

        private void SpawnEnemy(CharacterObject troop, Vec3 pos, Vec3 lookDir, Team enemyTeam)
        {
            Vec2 look2 = new(lookDir.x, lookDir.y);

            Agent agent = Mission.SpawnAgent(
                new AgentBuildData(troop)
                    .Team(enemyTeam)
                    .InitialPosition(pos)
                    .InitialDirection(look2)
                    .NoHorses(true)
                    .TroopOrigin(new SimpleAgentOrigin(troop))
                    .CivilianEquipment(false)
            );

            agent.Controller = Agent.ControllerType.AI;
            agent.SetWatchState(Agent.WatchState.Alarmed);
            agent.SetMorale(100f);
            agent.LookDirection = lookDir;
        }

        public override void OnAgentRemoved(Agent affected, Agent affector, AgentState state, KillingBlow blow)
        {
            if (affected?.Character is CharacterObject c && affector == Agent.Main)
            {
                _defeatedTroops.Add(c);
            }
        }

        public override void OnMissionTick(float dt)
        {
            // Wait until player is properly spawned
            if (!_playerSpawned)
            {
                if (Mission.MainAgent != null && Mission.MainAgent.IsActive())
                {
                    _playerSpawned = true;
                }
                return; // do NOT check defeat yet
            }

            var agent = Mission.MainAgent;

            // If player died
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
                return; // Don't check victory when player is dead
            }

            if (!_victoryShown)
            {
                bool anyEnemiesAlive = false;

                foreach (var a in Mission.Agents)
                {
                    if (a != null &&
                        a.Team == Mission.AttackerTeam &&
                        a != Mission.MainAgent &&
                        a.IsActive())
                    {
                        anyEnemiesAlive = true;
                        break;
                    }
                }

                if (!anyEnemiesAlive)
                {
                    MBInformationManager.AddQuickInformation(
                        new TextObject("Victory! Hold Tab to leave"), 3000
                    );
                    _victoryShown = true;
                }
            }
        }
    }
}
