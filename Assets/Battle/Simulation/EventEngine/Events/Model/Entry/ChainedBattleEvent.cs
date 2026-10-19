using Unity.Entities;

namespace Archeus.Battle.Buffers.Events
{
    public struct ChainedBattleEvent : IBufferElementData
    {
        public BattleEvent Event;
    }
}
