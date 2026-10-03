using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Runtime;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(DrawingStageGroup))]
    public partial struct DrawingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battleRNG, battle) in SystemAPI
                    .Query<RefRO<BattleState>, RefRW<BattleRNG>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleDrawingCompleteTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.Drawing)
                    continue;

                foreach (
                    var (ownedBattle, maxHandSize, team, player) in SystemAPI
                        .Query<RefRO<OwnedBattle>, RefRO<MaxHandSize>, RefRO<Team>>()
                        .WithAll<PlayerTag>()
                        .WithEntityAccess()
                )
                {
                    if (ownedBattle.ValueRO.Battle != battle)
                        continue;

                    if (
                        !SystemAPI.HasBuffer<DeckCard>(player)
                        || !SystemAPI.HasBuffer<HandCard>(player)
                    )
                    {
                        continue;
                    }

                    DynamicBuffer<DeckCard> deck = SystemAPI.GetBuffer<DeckCard>(player);
                    DynamicBuffer<HandCard> hand = SystemAPI.GetBuffer<HandCard>(player);

                    while (hand.Length < maxHandSize.ValueRO.Value && deck.Length > 0)
                    {
                        bool drawn = CardZoneResolver.TryDrawRandomCard(
                            ref battleRNG.ValueRW,
                            ref deck,
                            ref hand,
                            maxHandSize.ValueRO.Value,
                            out Entity drawnCard
                        );

                        if (!drawn)
                            break;
                        uint runtimeID = SystemAPI.GetComponent<CardRuntimeID>(drawnCard).Value;
                        Logging.Info(
                            LogCategory.Setup,
                            $"Card {runtimeID} drawn by {team.ValueRO.Side} (player {player.Index})."
                        );
                    }
                }

                ecb.AddComponent<BattleDrawingCompleteTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
