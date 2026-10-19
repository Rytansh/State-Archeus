using Unity.Entities;

namespace Archeus.Battle.Components.Ownership
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