using Unity.Entities;

namespace DBUS.Battle.Components.Combat
{
    public struct CharacterSlot : IComponentData
    {
        public int Value;
    }
    public struct SkillSlot : IComponentData
    {
        public int Value;
    }
    
}
