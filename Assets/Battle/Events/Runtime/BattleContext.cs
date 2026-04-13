using Unity.Entities;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Stats;
using Archeus.Content.Registries;

namespace Archeus.Battle.Events.Runtime
{
    public struct BattleContext
    {
        public Entity Battle;
        public DynamicBuffer<ChainedBattleEvent> ChainBuffer;

        public ComponentLookup<CharacterStats> StatsLookup;
        public ComponentLookup<CurrentHealth> HealthLookup;
        public BlobAssetReference<ContentBlobRegistry> BattleRegistryReference;
    }
}