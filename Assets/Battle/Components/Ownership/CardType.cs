using Unity.Entities;

namespace DBUS.Battle.Components.Ownership
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

