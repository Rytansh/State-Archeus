using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Components.Requests
{
    public struct CreateCharacterRequest : IComponentData
    {
        public Entity Battle;
        public Entity Owner;
        public uint CharacterDefinitionID;
    }
}
