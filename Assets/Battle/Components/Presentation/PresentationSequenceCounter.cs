using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public struct PresentationSequenceCounter : IComponentData
    {
        public uint NextSequence;
    }
}
