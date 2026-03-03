using Unity.Entities;

namespace DBUS.Battle.Components.Ownership
{
    public struct CardRuntimeID : IComponentData
    {
        public uint Value;
    }

    public struct CardDefinitionID : IComponentData
    {
        public uint Value;
    }
}