using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Setup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleSpawningGroup))]
    public partial struct BattleCreationCompletionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool creationRequestsExist =
                SystemAPI
                    .QueryBuilder()
                    .WithAll<CreateCharacterRequest>()
                    .Build()
                    .CalculateEntityCount() > 0;
            if (creationRequestsExist)
                return;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (battleState, battle) in SystemAPI
                    .Query<RefRO<BattleState>>()
                    .WithAll<BattleTag>()
                    .WithNone<BattleSpawningCompleteTag>()
                    .WithEntityAccess()
            )
            {
                if (battleState.ValueRO.Phase != BattlePhase.Spawning)
                    continue;

                ecb.AddComponent<BattleSpawningCompleteTag>(battle);
                ecb.AddComponent<BattleReadyTag>(battle);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
