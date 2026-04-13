using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Core;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Turnflow
{
    [UpdateInGroup(typeof(AttackingStageGroup))]
    public partial struct AttackingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battleState, battle) in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag>().WithNone<BattleAttackingCompleteTag>().WithEntityAccess())
            {
                if (battleState.ValueRO.Phase != BattlePhase.Attacking)
                    continue;

                ecb.AddComponent<BattleAttackingCompleteTag>(battle);
                Logging.Info(LogCategory.Combat, "Attacking stage complete.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
