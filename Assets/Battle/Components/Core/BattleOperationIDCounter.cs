using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleOperationIDCounter : IComponentData
    {
        public uint NextID;
    }
}
