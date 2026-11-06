using Archeus.Battle.Components.Tags;
using Archeus.Battle.Snapshots;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInputGroup))]
    [UpdateAfter(typeof(BattleStageRevertSystem))]
    public partial struct ActionControlSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            using var query = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattleTag>(),
                ComponentType.ReadOnly<ActionControlRequest>()
            );
            using NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp);
            foreach (Entity battle in battles)
                ActionControlProcessor.ApplyFrame(state.EntityManager, battle, 0);
        }
    }
}
