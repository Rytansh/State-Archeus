using System;
using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Events;
using DBUS.Battle.VM.Data;

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

            ecb.DestroyEntity(requestEntity); 
            Logging.System("[Battle] Battle created.");
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

        // EVENT RELATED COMPONENTS / BUFFERS
        ecb.AddComponent(battle, new BattleExecutionCounter { Value = 0 });
        ecb.AddBuffer<BattleEvent>(battle);
        ecb.AddBuffer<ChainedBattleEvent>(battle);
        ecb.AddBuffer<BehaviourExecutionRequest>(battle);
        ecb.AddBuffer<BattleExecutionLog>(battle);
    }

    private void AddComponentsToPlayer(ref SystemState state, EntityCommandBuffer ecb, Entity player, Entity battle)
    {
        //PLAYER RELATED COMPONENTS / BUFFERS
        ecb.AddComponent(player, new PlayerTag {});
        ecb.AddComponent(player, new OwnedBattle { Battle = battle });
    }

}

