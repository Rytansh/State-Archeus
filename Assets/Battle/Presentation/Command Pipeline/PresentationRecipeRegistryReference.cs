using Archeus.Battle.Presentation;
using Unity.Entities;

namespace Archeus.Battle.Components.Presentation
{
    public sealed class PresentationRecipeRegistryReference : IComponentData
    {
        public PresentationRecipeRegistry Registry;
    }

    public struct PresentationPacketCompiledTag : IComponentData { }
}
