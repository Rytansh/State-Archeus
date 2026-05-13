using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Components.Requests
{
    public struct SpawnCharacterRequest : IComponentData
    {
        public Entity Battle;
        public BattleSide Side;
        public int Slot;
        public uint CharacterID;
    }
}

