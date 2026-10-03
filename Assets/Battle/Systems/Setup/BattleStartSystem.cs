using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    public partial struct BattleStartSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleTurnStartTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.BattleReady)
                    continue;

                ecb.AddComponent<BattleTurnStartTag>(battle);

                Logging.Info(LogCategory.Setup, "Battle starting.");
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
