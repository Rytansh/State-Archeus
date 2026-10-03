using Unity.Entities;

namespace Archeus.Battle.Buffers.Combat
{
    public struct BattleParticipant : IBufferElementData
    {
        public Entity Participant;
    }
}