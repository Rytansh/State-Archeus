using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Events;

namespace Archeus.Battle.Events.Runtime
{
    public struct EventFrame
    {
        public BattleEvent Event;
        public BattleEventPhase Phase;
        public bool PhaseStarted;
    }
}