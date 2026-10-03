using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct CharacterVisibleState : IComponentData
    {
        public float Health;

        public bool IsAlive;
    }
}
