using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct HandPresentationState : IComponentData
    {
        public BattleSide Side;
        public int MaxSize;
    }
}
