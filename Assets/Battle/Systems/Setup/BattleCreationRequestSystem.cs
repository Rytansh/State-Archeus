using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    public partial struct BattleCreationRequestSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleSpawnRequestsIssuedTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.Initialising)
                    continue;

                foreach (
                    var (ownedBattle, player) in SystemAPI
                        .Query<RefRO<OwnedBattle>>()
                        .WithAll<PlayerTag>()
                        .WithEntityAccess()
                )
                {
                    if (ownedBattle.ValueRO.Battle != battle)
                        continue;

                    DynamicBuffer<BattleDeckEntry> deck = SystemAPI.GetBuffer<BattleDeckEntry>(
                        player
                    );

                    for (int i = 0; i < deck.Length; i++)
                    {
                        BattleDeckEntry entry = deck[i];

                        switch (entry.CardType)
                        {
                            case RuntimeCardType.Character:
                            {
                                Entity requestEntity = ecb.CreateEntity();

                                ecb.AddComponent(
                                    requestEntity,
                                    new CreateCharacterRequest
                                    {
                                        Battle = battle,
                                        Owner = player,
                                        CharacterDefinitionID = entry.CardDefinitionID,
                                    }
                                );

                                break;
                            }

                            case RuntimeCardType.Skill:
                            {
                                // Later:
                                // CreateSkillRequest
                                break;
                            }
                        }
                    }
                }

                ecb.AddComponent<BattleSpawnRequestsIssuedTag>(battle);
                Logging.Info(LogCategory.Setup, "Battle spawn requests issued.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
