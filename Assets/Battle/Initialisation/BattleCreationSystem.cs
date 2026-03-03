using System;
using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Requests;
using DBUS.Battle.Components.Determinism;
using DBUS.Battle.Components.Ownership;
using DBUS.Battle.Components.Events;

[UpdateInGroup(typeof(BattleCreationGroup))]
public partial struct BattleCreationSystem : ISystem 
{ 
    public void OnUpdate(ref SystemState state) 
    { 
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp); 
        foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<StartBattleRequest>>().WithEntityAccess()) 
        { 
            Entity battleEntity = ecb.CreateEntity(); 
            ecb.AddComponent<BattleTag>(battleEntity);
            var rng = new DeterministicRNG(request.ValueRO.BattleSeed);
            ecb.AddComponent(battleEntity, new BattleRNG
            {
                StateA = rng.StateA,
                StateB = rng.StateB
            });
            ecb.AddComponent(battleEntity, new BattleState { Phase = BattlePhase.Creating });
            ecb.AddComponent<BattleEventProcessingState>(battleEntity);
            ecb.AddBuffer<RegisteredTrigger>(battleEntity);
            ecb.AddBuffer<BattleEventBuffer>(battleEntity);
            ecb.AddBuffer<BehaviorExecutionRequest>(battleEntity);
            ecb.AddComponent(battleEntity, new BattleExecutionCounter { Value = 0 });
            ecb.AddBuffer<BattleExecutionLog>(battleEntity);

            Entity player = ecb.CreateEntity();
            ecb.AddComponent(player, new PlayerTag {});
            ecb.AddComponent(player, new OwnedBattle { Battle = battleEntity }); 

            ecb.DestroyEntity(requestEntity); 

            Logging.System("[Battle] Battle created.");
        } 
        
        ecb.Playback(state.EntityManager); 
        ecb.Dispose(); } }

