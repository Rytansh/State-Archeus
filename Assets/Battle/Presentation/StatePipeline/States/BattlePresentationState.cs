using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct BattlePresentationState : IComponentData
    {
        public ulong BattleRuntimeID;
        public int TurnNumber;
        public BattlePhase Phase;
        public BattleSide ActiveSide;
    }
}
