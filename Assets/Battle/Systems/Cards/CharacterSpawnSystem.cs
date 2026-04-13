using Unity.Entities; 
using Unity.Collections; 
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Content.Registries;
using Archeus.Content.Blobs;
using Archeus.Content.Lookup;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Cards
{
    [UpdateInGroup(typeof(BattleSetupGroup))] 
    public partial struct CharacterSpawnSystem : ISystem 
    { 
        public void OnCreate(ref SystemState state) 
        { 
            state.RequireForUpdate<ContentLookupTables>(); 
        } 
        public void OnUpdate(ref SystemState state) 
        { 
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp); 
            ContentLookupTables lookup = SystemAPI.GetSingleton<ContentLookupTables>(); 

            foreach (var (request, requestEntity) in SystemAPI.Query<RefRO<SpawnCharacterRequest>>().WithEntityAccess()) 
            { 
                Entity battle = request.ValueRO.Battle;
                Entity character = ecb.CreateEntity(); 

                if (!SystemAPI.HasComponent<BattleContentRegistry>(battle)) {ecb.DestroyEntity(requestEntity); continue;}

                // CREATE RCIS
                BattleContentRegistry battleContent = SystemAPI.GetComponent<BattleContentRegistry>(battle);
                BlobAssetReference<ContentBlobRegistry> registryRef = battleContent.BattleRegistryReference;
                ref ContentBlobRegistry registry = ref registryRef.Value;
                RefRW<BattleRuntimeIDCounter> counter = SystemAPI.GetComponentRW<BattleRuntimeIDCounter>(battle); 

                // INCREMENT ID COUNTER FOR ASSET
                uint runtimeID = counter.ValueRO.NextID; 
                counter.ValueRW.NextID++; 

                // GET CHARACTER ASSET
                int characterIndex = lookup.CharacterIDToIndex[request.ValueRO.CharacterID]; 
                ref CharacterDefinitionBlob characterDef = ref registry.Characters[characterIndex]; 

                // BUILD CHARACTER COMPONENTS
                BuildCharacter(ecb, character, battle, runtimeID, request, lookup, ref registry);

                Logging.Info(LogCategory.Setup, "Spawned character " + characterDef.ID + " with runtime ID " + runtimeID + " successfully. (Attack: " + characterDef.CharacterBlobBaseStats.Attack + ")");
                ecb.DestroyEntity(requestEntity); 
            } 
            ecb.Playback(state.EntityManager); 
            ecb.Dispose(); 
        } 

        private void BuildCharacter(EntityCommandBuffer ecb, Entity character, Entity battle, uint runtimeID, RefRO<SpawnCharacterRequest> request, ContentLookupTables lookup, ref ContentBlobRegistry registry)
        {
            int characterIndex = lookup.CharacterIDToIndex[request.ValueRO.CharacterID]; 
            ref CharacterDefinitionBlob characterDef = ref registry.Characters[characterIndex];  

            ecb.AddComponent(character, new CharacterTag { Battle = battle }); 
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

            DynamicBuffer<BehaviourReference> behaviourReferenceBuffer = ecb.AddBuffer<BehaviourReference>(character); 
            DynamicBuffer<BehaviourRuntimeState> behaviourStateBuffer = ecb.AddBuffer<BehaviourRuntimeState>(character);
            for (int i = 0; i < characterDef.BehaviourIDs.Length; i++) 
            { 
                uint behaviourID = characterDef.BehaviourIDs[i]; 
                int behaviourIndex = lookup.BehaviourIDToIndex[behaviourID]; 
                behaviourReferenceBuffer.Add(new BehaviourReference { BehaviourIndex = behaviourIndex });
                BehaviourRuntimeState behaviourState = new BehaviourRuntimeState{ Memory = default };
                behaviourStateBuffer.Add(behaviourState);
            } 
        }
    }
}

