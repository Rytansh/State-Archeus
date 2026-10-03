using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public struct BattleContentReadyTag : IComponentData { }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ContentLookupSystem))]
    public partial struct BattleContentHandlingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContentBlobRegistryComponent>();
            state.RequireForUpdate<ContentLookupTables>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<BattleContentReadyTag>())
            {
                state.Enabled = false;
                return;
            }

            state.EntityManager.CreateEntity(typeof(BattleContentReadyTag));

            Logging.Info(
                LogCategory.System,
                $"Simulation content is ready in world: " + $"{state.WorldUnmanaged.Name}."
            );

            // One-shot readiness system.
            state.Enabled = false;
        }
    }
}
