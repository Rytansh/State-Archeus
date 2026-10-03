using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleActionExecutionCounter : IComponentData
    {
        public uint NextID;
    }
}
