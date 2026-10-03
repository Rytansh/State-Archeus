using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Turnflow
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(AttackingStageGroup))]
    public partial struct AttackingStageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleAttackingCompleteTag>()
                    .WithEntityAccess()
            )
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
