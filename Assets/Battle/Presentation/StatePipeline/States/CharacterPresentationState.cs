using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct CharacterPresentationState : IComponentData
    {
        public uint RuntimeID;

        public float CurrentHealth;
        public float MaxHealth;

        public bool IsAlive;
    }
}
