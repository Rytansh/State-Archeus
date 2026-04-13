using Unity.Entities;

namespace Archeus.Battle.Components.Tags
{
    public struct CharacterTag : IComponentData
    {
        public Entity Battle;
    }

    public struct SkillTag : IComponentData
    {
        public Entity Battle;
    }
}

