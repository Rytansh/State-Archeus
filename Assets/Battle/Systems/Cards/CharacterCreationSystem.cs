using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Content.Blobs;
using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Cards
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleSetupGroup))]
    public partial struct CharacterCreationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContentLookupTables>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            ContentLookupTables lookup = SystemAPI.GetSingleton<ContentLookupTables>();

            foreach (
                var (request, requestEntity) in SystemAPI
                    .Query<RefRO<CreateCharacterRequest>>()
                    .WithEntityAccess()
            )
            {
                CreateCharacterRequest requestData = request.ValueRO;

                Entity battle = requestData.Battle;
                Entity owner = requestData.Owner;

                if (!SystemAPI.HasComponent<BattleContentRegistry>(battle))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                if (!SystemAPI.HasComponent<Team>(owner))
                {
                    ecb.DestroyEntity(requestEntity);
                    continue;
                }

                BattleContentRegistry battleContent = SystemAPI.GetComponent<BattleContentRegistry>(
                    battle
                );

                ref ContentBlobRegistry registry = ref battleContent.BattleRegistryReference.Value;

                int characterIndex = lookup.CharacterIDToIndex[requestData.CharacterDefinitionID];

                ref CharacterDefinitionBlob definition = ref registry.Characters[characterIndex];

                Team ownerTeam = SystemAPI.GetComponent<Team>(owner);

                uint runtimeID = AllocateRuntimeID(ref state, battle);

                Entity character = ecb.CreateEntity();

                BuildCharacter(
                    ecb,
                    character,
                    in requestData,
                    runtimeID,
                    in ownerTeam,
                    ref definition
                );

                ecb.AppendToBuffer(owner, new DeckCard { Card = character });
                Logging.Info(
                    LogCategory.Testing,
                    $"Character with ID {runtimeID} is ready in the deck."
                );

                ecb.DestroyEntity(requestEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void BuildCharacter(
            EntityCommandBuffer ecb,
            Entity character,
            in CreateCharacterRequest request,
            uint runtimeID,
            in Team ownerTeam,
            ref CharacterDefinitionBlob definition
        )
        {
            ecb.AddComponent<CharacterTag>(character);

            ecb.AddComponent(
                character,
                new CardDefinitionID { Value = request.CharacterDefinitionID }
            );

            ecb.AddComponent(character, new CardRuntimeID { Value = runtimeID });

            ecb.AddComponent(character, new CardOwner { Player = request.Owner });

            ecb.AddComponent(character, new OwnedBattle { Battle = request.Battle });

            ecb.AddComponent(character, ownerTeam);

            ecb.AddComponent(
                character,
                new CharacterStats
                {
                    Attack = definition.CharacterBlobBaseStats.Attack,
                    Defense = definition.CharacterBlobBaseStats.Defense,
                    MaxHealth = definition.CharacterBlobBaseStats.MaxHealth,
                    CritRATE = definition.CharacterBlobBaseStats.CritRATE,
                    CritDMG = definition.CharacterBlobBaseStats.CritDMG,
                }
            );

            ecb.AddComponent(
                character,
                new CurrentHealth { Value = definition.CharacterBlobBaseStats.MaxHealth }
            );

            DynamicBuffer<BehaviourReference> behaviours = ecb.AddBuffer<BehaviourReference>(
                character
            );

            DynamicBuffer<BehaviourRuntimeState> runtimeStates =
                ecb.AddBuffer<BehaviourRuntimeState>(character);

            ecb.AddBuffer<ActiveEffect>(character);

            for (int i = 0; i < definition.BehaviourIndices.Length; i++)
            {
                behaviours.Add(
                    new BehaviourReference { BehaviourIndex = definition.BehaviourIndices[i] }
                );

                runtimeStates.Add(new BehaviourRuntimeState { Memory = default });
            }
        }

        private uint AllocateRuntimeID(ref SystemState state, Entity battle)
        {
            RefRW<BattleRuntimeIDCounter> counter =
                SystemAPI.GetComponentRW<BattleRuntimeIDCounter>(battle);

            uint id = counter.ValueRO.NextID;
            counter.ValueRW.NextID++;

            return id;
        }
    }
}
