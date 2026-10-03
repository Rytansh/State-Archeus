using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Content.Lookup
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ContentLookupSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ContentBlobRegistryComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.HasSingleton<ContentLookupTables>())
            {
                state.Enabled = false;
                return;
            }
            var registryRef = SystemAPI
                .GetSingleton<ContentBlobRegistryComponent>()
                .BlobRegistryReference;
            ref var registry = ref registryRef.Value;

            var characterMap = RegisterToLookupTable(ref registry.Characters);
            var skillMap = RegisterToLookupTable(ref registry.Skills);
            var behaviourMap = RegisterToLookupTable(ref registry.Behaviours);
            var effectMap = RegisterToLookupTable(ref registry.Effects);

            var lookupEntity = state.EntityManager.CreateEntity(typeof(ContentLookupTables));
            state.EntityManager.AddComponentData(
                lookupEntity,
                new ContentLookupTables
                {
                    CharacterIDToIndex = characterMap,
                    SkillIDToIndex = skillMap,
                    BehaviourIDToIndex = behaviourMap,
                    EffectIDToIndex = effectMap,
                }
            );
            Logging.Info(
                LogCategory.System,
                $"Lookup system initialised successfully in world: {state.WorldUnmanaged.Name}."
            );
            // One-shot system
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<ContentLookupTables>())
                return;

            ref ContentLookupTables lookups = ref SystemAPI
                .GetSingletonRW<ContentLookupTables>()
                .ValueRW;

            lookups.Dispose();

            Logging.Info(
                LogCategory.System,
                $"Content lookup tables disposed in world: " + $"{state.WorldUnmanaged.Name}."
            );
        }

        private NativeHashMap<uint, int> RegisterToLookupTable<T>(ref BlobArray<T> items)
            where T : struct, IHasID
        {
            var map = new NativeHashMap<uint, int>(items.Length, Allocator.Persistent);

            for (int i = 0; i < items.Length; i++)
            {
                uint id = items[i].GetID();

                if (!map.TryAdd(id, i))
                {
                    Logging.Error(
                        LogCategory.System,
                        "Duplicate asset found. Not registered into the lookup system."
                    );
                }
            }

            return map;
        }
    }

    public interface IHasID
    {
        uint GetID();
    }
}
