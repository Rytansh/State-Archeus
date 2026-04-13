using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Content.Registries;
using Archeus.Game.Bootstrap;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Setup
{
    [UpdateInGroup(typeof(BattleCreationGroup))]
    public partial struct BattleCreationSystem : ISystem 
    { 
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StartBattleRequest>();
        }
        public void OnUpdate(ref SystemState state) 
        { 
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<StartBattleRequest>>().WithEntityAccess()) 
            { 
                // BATTLE RELATED CREATION
                Entity battleEntity = ecb.CreateEntity(); 
                DeterministicRNG rng = new DeterministicRNG(request.ValueRO.BattleSeed);
                AddComponentsToBattle(ref state, ecb, battleEntity, rng);

                // PLAYER RELATED CREATION
                Entity player = ecb.CreateEntity();
                AddComponentsToPlayer(ref state, ecb, player, battleEntity);

                //DESTROY BATTLE START REQUEST ENTITY
                ecb.DestroyEntity(requestEntity); 
                Logging.Info(LogCategory.Setup, "Battle created.");
            } 
            
            ecb.Playback(state.EntityManager); 
            ecb.Dispose(); 
        } 

        private void AddComponentsToBattle(ref SystemState state, EntityCommandBuffer ecb, Entity battle, DeterministicRNG rng)
        {
            // BATTLE RELATED COMPONENTS / BUFFERS
            ecb.AddComponent<BattleTag>(battle);
            ecb.AddComponent(battle, new BattleRNG {StateA = rng.StateA, StateB = rng.StateB});
            ecb.AddComponent(battle, new BattleState { Phase = BattlePhase.Creating });
            ecb.AddComponent(battle, new BattleRuntimeIDCounter { NextID = 100 });
            ecb.AddComponent(battle, new BattleContentRegistry { BattleRegistryReference = SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference });

            // EVENT RELATED COMPONENTS / BUFFERS
            ecb.AddBuffer<BattleEvent>(battle);
            ecb.AddBuffer<ChainedBattleEvent>(battle);
            ecb.AddBuffer<BehaviourExecutionRequest>(battle);
        }

        private void AddComponentsToPlayer(ref SystemState state, EntityCommandBuffer ecb, Entity player, Entity battle)
        {
            //PLAYER RELATED COMPONENTS / BUFFERS
            ecb.AddComponent(player, new PlayerTag {});
            ecb.AddComponent(player, new OwnedBattle { Battle = battle });
        }

    }
}

