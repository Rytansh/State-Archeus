using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Combat;

namespace Archeus.Battle.Systems.Turnflow
{
    [UpdateInGroup(typeof(DrawingStageGroup))]
    public partial struct DrawingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (battleState, battle) in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag>().WithNone<BattleDrawingCompleteTag>().WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.Drawing)
                    continue;

                foreach (var (ownedBattle, hand, max, player) in SystemAPI.Query<RefRO<OwnedBattle>,RefRW<PlayerHand>,RefRO<MaxHandSize>>().WithAll<PlayerTag>().WithEntityAccess())
                {
                    if (ownedBattle.ValueRO.Battle != battle)
                        continue;
                    hand.ValueRW.Current = max.ValueRO.Value;
                }
                ecb.AddComponent<BattleDrawingCompleteTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
