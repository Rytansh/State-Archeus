using Unity.Entities;
using DBUS.Battle.Components.Combat;

namespace DBUS.Battle.VM.Data
{
    public struct AbilityExecutionContext
    {
        public DynamicBuffer<ChainedBattleEvent> ChainedEventQueue;
        public ComponentLookup<CharacterStats> CharacterStatsLookup;

        public BlobAssetReference<ContentBlobRegistry> ContentRegistry;
    }
}