using Unity.Entities;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Buffers.Events;
using Archeus.Content.Registries;

namespace Archeus.Battle.VM.Execution
{
    public struct AbilityExecutionContext
    {
        public DynamicBuffer<ChainedBattleEvent> ChainedEventQueue;
        public ComponentLookup<ResolvedCharacterStats> CharacterStatsLookup;

        public BlobAssetReference<ContentBlobRegistry> ContentRegistry;
        public DynamicBuffer<BehaviourRuntimeState> StateBuffer;
        public int StateIndex;
    }
}