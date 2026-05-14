using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Cards
{
    [UpdateInGroup(typeof(PlanningStageGroup))]
    public partial struct TargetSelectionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CycleTargetRequest>>().WithEntityAccess())
            {
                Entity player = request.ValueRO.Player;

                if (!SystemAPI.HasComponent<SelectedTarget>(player) || !SystemAPI.HasComponent<Team>(player))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                Team playerTeam = SystemAPI.GetComponent<Team>(player);
                RefRW<SelectedTarget> selectedTarget = SystemAPI.GetComponentRW<SelectedTarget>(player);

                NativeList<Entity> validTargets = new NativeList<Entity>(Allocator.Temp);

                // COLLECT VALID ENEMY TARGETS
                foreach (var (team, hp, entity) in SystemAPI.Query<RefRO<Team>, RefRO<CurrentHealth>>().WithAll<CharacterTag>().WithEntityAccess())
                {
                    // Ignore same-team entities
                    if (team.ValueRO.Side == playerTeam.Side)
                        continue;

                    // Ignore dead entities
                    if (hp.ValueRO.Value <= 0f)
                        continue;

                    validTargets.Add(entity);
                }

                // No valid targets
                if (validTargets.Length == 0)
                {
                    selectedTarget.ValueRW.Value = Entity.Null;

                    validTargets.Dispose();
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                int currentIndex = -1;

                // Find current target index
                for (int i = 0; i < validTargets.Length; i++)
                {
                    if (validTargets[i] == selectedTarget.ValueRO.Value)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                // If current target invalid/null, start at 0
                if (currentIndex == -1)
                {
                    currentIndex = 0;
                }
                else
                {
                    currentIndex += request.ValueRO.Direction;

                    if (currentIndex >= validTargets.Length)
                        currentIndex = 0;

                    if (currentIndex < 0)
                        currentIndex = validTargets.Length - 1;
                }

                selectedTarget.ValueRW.Value = validTargets[currentIndex];
                Logging.Info(LogCategory.Testing, $"Selected target is {selectedTarget.ValueRW.Value.Index}");

                validTargets.Dispose();
                ecb.DestroyEntity(requestEntity);
            }

            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<CycleCharacterRequest>>().WithEntityAccess())
            {
                Entity player = request.ValueRO.Player;

                if (!SystemAPI.HasComponent<SelectedCharacter>(player) || !SystemAPI.HasComponent<Team>(player))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                Team playerTeam = SystemAPI.GetComponent<Team>(player);
                RefRW<SelectedCharacter> selectedTarget = SystemAPI.GetComponentRW<SelectedCharacter>(player);

                NativeList<Entity> validTargets = new NativeList<Entity>(Allocator.Temp);

                // COLLECT VALID ENEMY TARGETS
                foreach (var (team, hp, entity) in SystemAPI.Query<RefRO<Team>, RefRO<CurrentHealth>>().WithAll<CharacterTag>().WithEntityAccess())
                {
                    if (team.ValueRO.Side != playerTeam.Side)
                        continue;

                    if (hp.ValueRO.Value <= 0f)
                        continue;

                    validTargets.Add(entity);
                }

                // No valid targets
                if (validTargets.Length == 0)
                {
                    selectedTarget.ValueRW.Value = Entity.Null;

                    validTargets.Dispose();
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                int currentIndex = -1;

                // Find current target index
                for (int i = 0; i < validTargets.Length; i++)
                {
                    if (validTargets[i] == selectedTarget.ValueRO.Value)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                // If current target invalid/null, start at 0
                if (currentIndex == -1)
                {
                    currentIndex = 0;
                }
                else
                {
                    currentIndex += request.ValueRO.Direction;

                    if (currentIndex >= validTargets.Length)
                        currentIndex = 0;

                    if (currentIndex < 0)
                        currentIndex = validTargets.Length - 1;
                }

                selectedTarget.ValueRW.Value = validTargets[currentIndex];
                Logging.Info(LogCategory.Testing, $"Selected character is {selectedTarget.ValueRW.Value.Index}");

                validTargets.Dispose();
                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}