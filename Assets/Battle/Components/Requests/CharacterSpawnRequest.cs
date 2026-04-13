using Unity.Entities;

namespace Archeus.Battle.Components.Requests
{
    public struct SpawnCharacterRequest : IComponentData
    {
        public Entity Battle;
        public int Slot;
        public uint CharacterID;
    }
}

