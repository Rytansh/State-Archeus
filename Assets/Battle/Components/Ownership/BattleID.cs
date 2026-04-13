using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
{
    public struct BattleID: IComponentData
    {        
        public ulong Value;
    }
}