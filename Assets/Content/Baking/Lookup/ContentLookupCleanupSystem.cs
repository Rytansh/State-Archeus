using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Content.Lookup
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ContentLookupSystem))]
    public partial struct ContentLookupCleanupSystem : ISystem
    {
        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ContentLookupTables>())
                return;

            ref var lookups =
                ref SystemAPI.GetSingletonRW<ContentLookupTables>().ValueRW;

            lookups.Dispose();

            Logging.Info(LogCategory.System, "Content lookup tables disposed.");
        }
    }
}


