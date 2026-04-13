using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleRuntimeIDCounter: IComponentData
    {        
        public uint NextID;
    }
}