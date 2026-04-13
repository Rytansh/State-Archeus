using Unity.Collections;
using Unity.Entities;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;

namespace Archeus.Content.Lookup
{
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
            var registryRef = SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference;
            ref var registry = ref registryRef.Value;

            var characterMap = RegisterToLookupTable(ref registry.Characters);
            var skillMap = RegisterToLookupTable(ref registry.Skills);
            var behaviourMap = RegisterToLookupTable(ref registry.Behaviours);

            var lookupEntity = state.EntityManager.CreateEntity(typeof(ContentLookupTables));
            state.EntityManager.AddComponentData(
                lookupEntity,
                new ContentLookupTables
                {
                    CharacterIDToIndex = characterMap,
                    SkillIDToIndex = skillMap,
                    BehaviourIDToIndex = behaviourMap
                }
            );
            Logging.Info(LogCategory.System, $"Lookup system initialised successfully.");
            // One-shot system
            state.Enabled = false;
        }

        private NativeHashMap<uint, int> RegisterToLookupTable<T>(ref BlobArray<T> items) where T : struct, IHasID
        {
            var map = new NativeHashMap<uint, int>(items.Length, Allocator.Persistent);

            for (int i = 0; i < items.Length; i++)
            {
                uint id = items[i].GetID();

                if (!map.TryAdd(id, i))
                {
                    Logging.Error(LogCategory.System, "Duplicate asset found. Not registered into lookup.");
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


