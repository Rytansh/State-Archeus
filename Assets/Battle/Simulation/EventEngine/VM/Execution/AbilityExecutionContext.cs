using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Events.Context;
using Archeus.Content.Registries;
using Unity.Entities;

namespace Archeus.Battle.VM.Execution
{
    public struct AbilityExecutionContext
    {
        public DynamicBuffer<ChainedBattleEvent> ChainedEventQueue;
        public ComponentLookup<CharacterStats> CharacterStatsLookup;
        public DynamicBuffer<ActionExecutionState> ActionExecutionStates;
        public RefRW<BattleOperationIDCounter> OperationCounter;

        public DynamicBuffer<BattleParticipant> BattleParticipants;
        public ComponentLookup<Team> TeamLookup;
        public ComponentLookup<CurrentHealth> CurrentHealthLookup;

        public BlobAssetReference<ContentBlobRegistry> ContentRegistry;
        public DynamicBuffer<BehaviourRuntimeState> StateBuffer;
        public int StateIndex;

        public EventEmissionContext EmissionContext;
    }
}
