using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public struct PresentationStateRevision : IComponentData
    {
        public ulong Value;
    }

    public struct InitialPresentationStatePublishedTag : IComponentData { }
}
