using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct PlayerPresentationState : IComponentData
    {
        public BattleSide Side;
        public int ActionPoints;
    }
}
