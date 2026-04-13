using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleRNG: IComponentData
    {
        public ulong StateA;
        public ulong StateB;
    }

}
