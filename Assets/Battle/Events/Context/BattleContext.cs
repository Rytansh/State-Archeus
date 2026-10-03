using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Stats;
using Archeus.Content.Registries;
using Unity.Entities;

namespace Archeus.Battle.Events.Context
{
    public struct BattleContext
    {
        public Entity Battle;
        public ulong BattleID;

        // Battle-owned event resources
        public DynamicBuffer<ChainedBattleEvent> ChainedEventQueue;
        public DynamicBuffer<BattleParticipant> Participants;
        public DynamicBuffer<ActionExecutionState> ActionExecutionStates;
        public RefRW<BattleOperationIDCounter> OperationCounter;

        // Battle-owned presentation resources
        public DynamicBuffer<PresentationFact> PresentationFactQueue;
        public RefRW<PresentationSequenceCounter> PresentationSequenceCounter;

        // Entity-indexed runtime data
        public ComponentLookup<CharacterStats> StatsLookup;
        public ComponentLookup<CurrentHealth> HealthLookup;
        public ComponentLookup<Team> TeamLookup;
        public ComponentLookup<CardRuntimeID> CardRuntimeIDLookup;

        public BufferLookup<ActiveEffect> EffectLookup;

        // Battle-specific runtime data
        public ComponentLookup<BattleRNG> RNGLookup;

        public BlobAssetReference<ContentBlobRegistry> BattleRegistryReference;
    }
}
