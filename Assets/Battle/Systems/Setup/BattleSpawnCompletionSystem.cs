using Unity.Entities;
using Unity.Collections;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Core;
using Archeus.Core.Debugging;

namespace Archeus.Battle.Systems.Setup
{
    [UpdateInGroup(typeof(BattleSpawningGroup))]
    public partial struct BattleSpawnCompletionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            bool spawnRequestsExist = SystemAPI.QueryBuilder().WithAll<SpawnCharacterRequest>().Build().CalculateEntityCount() > 0;
            if (spawnRequestsExist) return;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (battleState, battle)in SystemAPI.Query<RefRO<BattleState>>().WithAll<BattleTag>().WithNone<BattleSpawningCompleteTag>().WithEntityAccess())
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


