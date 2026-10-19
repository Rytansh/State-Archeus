using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Data.Events;

namespace Archeus.Battle.Events.Context
{
    public struct EventFrame
    {
        public uint ID;
        public BattleEvent Event;
        public BattleEventPhase Phase;
        public bool PhaseStarted;
        public ushort PendingVMContinuations;
        public bool Completed;
    }
}
