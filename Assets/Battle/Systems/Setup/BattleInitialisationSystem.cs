using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Combat;

namespace Archeus.Battle.Systems.Setup
{
    [UpdateInGroup(typeof(BattleInitialisationGroup))]
    public partial struct BattleInitialisationSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battleState, battle) in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag>().WithNone<BattleInitialisationCompleteTag>().WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.Creating)
                    continue;

                // INITIALISE BATTLE RELATED INFORMATION
                ecb.AddComponent(battle, new TurnCounter { CurrentTurn = 0 });

                foreach (var (ownedBattle, player) in SystemAPI.Query<RefRO<OwnedBattle>>().WithAll<PlayerTag>().WithEntityAccess())
                {
                    if (ownedBattle.ValueRO.Battle != battle)
                        continue;

                    // INITIALISE PLAYER FUNCTIONS
                    ecb.AddComponent(player, new MaxActionPoints { Value = 4 });
                    ecb.AddComponent(player, new RemainingActionPoints { Value = 4 });
                    ecb.AddComponent(player, new PlayerHand { Current = 0 });
                    ecb.AddComponent(player, new MaxHandSize { Value = 4 });
                }

                ecb.AddComponent<BattleInitialisationCompleteTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    }

