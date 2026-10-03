using Archeus.Battle.Presentation.Facts;
using Unity.Entities;

namespace Archeus.Battle.Buffers.Presentation
{
    public struct PresentationFact : IBufferElementData
    {
        public PresentationFactType FactType;
        public PresentationFactMetadata FactMetadata;
        public PresentationFactPayload FactPayload;
    }
}
