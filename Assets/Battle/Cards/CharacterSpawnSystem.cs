using Unity.Entities; 
using Unity.Collections; 
using DBUS.Battle.Components.Combat; 
using DBUS.Battle.Components.Requests; 
using DBUS.Battle.Components.Ownership; 
using DBUS.Battle.Components.Determinism; 

[UpdateInGroup(typeof(BattleSetupGroup))] 
public partial struct CharacterSpawnSystem : ISystem 
{ 
    public void OnCreate(ref SystemState state) 
    { 
        state.RequireForUpdate<ContentBlobRegistryComponent>(); 
        state.RequireForUpdate<ContentLookupTables>(); 
    } 
    public void OnUpdate(ref SystemState state) 
    { 
        var registryRef = SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference; 
        ref var registry = ref registryRef.Value; 
        EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp); 

        foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<SpawnCharacterRequest>>() .WithEntityAccess()) 
        { 
            var lookup = SystemAPI.GetSingleton<ContentLookupTables>(); 
            Entity character = ecb.CreateEntity(); 

            var counter = SystemAPI.GetComponentRW<BattleRuntimeIDCounter>(request.ValueRO.Battle); 
            uint runtimeID = counter.ValueRO.NextID; 
            counter.ValueRW.NextID++; 

            int characterIndex = lookup.CharacterIDToIndex[request.ValueRO.CharacterID]; 

            ref var characterDef = ref registry.Characters[characterIndex]; 

            ecb.AddComponent(character, new CharacterTag { Battle = request.ValueRO.Battle }); 
            ecb.AddComponent(character, new CardDefinitionID { Value = request.ValueRO.CharacterID}); 
            ecb.AddComponent(character, new CardRuntimeID { Value = runtimeID}); 
            ecb.AddComponent(character, new CharacterSlot { Value = request.ValueRO.Slot }); 
            ecb.AddComponent(character, new CharacterStats 
            { 
                Attack = characterDef.CharacterBlobBaseStats.Attack, 
                Defense = characterDef.CharacterBlobBaseStats.Defense, 
                MaxHealth = characterDef.CharacterBlobBaseStats.MaxHealth 
            }); 
            ecb.AddComponent(character, new CurrentHealth { Value = characterDef.CharacterBlobBaseStats.MaxHealth }); 
            
            var behaviourReferenceBuffer = ecb.AddBuffer<BehaviourReference>(character); 
            var behaviourStateBuffer = ecb.AddBuffer<BehaviourRuntimeState>(character);
            for (int i = 0; i < characterDef.BehaviourIDs.Length; i++) 
            { 
                uint behaviourID = characterDef.BehaviourIDs[i]; 
                int behaviourIndex = lookup.BehaviourIDToIndex[behaviourID]; 
                behaviourReferenceBuffer.Add(new BehaviourReference { BehaviourIndex = behaviourIndex });
                var behaviourState = new BehaviourRuntimeState
                {
                    Memory = default
                };
                behaviourStateBuffer.Add(behaviourState);
            } 

            Logging.System("[Setup] Spawned character " + characterDef.ID + " with runtime ID " + runtimeID + " successfully. (Attack: " + characterDef.CharacterBlobBaseStats.Attack + ")");
            
            ecb.DestroyEntity(requestEntity); 
        } 
        ecb.Playback(state.EntityManager); 
        ecb.Dispose(); 
    } 
}

