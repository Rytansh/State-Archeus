using Archeus.Battle.Components.Core;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    public static class OperationIDAllocator
    {
        public static uint Allocate(RefRW<BattleOperationIDCounter> counter)
        {
            uint id = counter.ValueRO.NextID;

            counter.ValueRW.NextID++;

            return id;
        }
    }
}
