using Archeus.Battle.Presentation;
using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public sealed class PresentationRegistryReference : IComponentData
    {
        public PresentationRegistry Registry;
    }
}
