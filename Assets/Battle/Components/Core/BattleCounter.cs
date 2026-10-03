using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleRuntimeIDCounter: IComponentData
    {        
        public uint NextID;
    }
    public struct BattleEventFrameIDCounter: IComponentData
    {
        public uint NextID;
    }

    public struct BattleEventGroupIDCounter: IComponentData
    {
        public uint NextID;
    }
}