using Archeus.Content.Lookup;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Entities;

[UpdateAfter(typeof(ContentLookupSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct RegistryLookupConsistencyTest : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ContentLookupTables>();
        state.RequireForUpdate<ContentBlobRegistryComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ref var lookups =
            ref SystemAPI.GetSingletonRW<ContentLookupTables>().ValueRW;

        ref var registry = ref SystemAPI.GetSingleton<ContentBlobRegistryComponent>().BlobRegistryReference.Value;

        int characterCount = registry.Characters.Length;
        int errors = 0;

        foreach (var pair in lookups.CharacterIDToIndex)
        {
            uint id = pair.Key;
            int index = pair.Value;

            if ((uint)index >= characterCount)
            {
                Logging.Error(LogCategory.Testing,
                    $"[RegistryLookupTest] Index out of range. ID={id}, Index={index}, Count={characterCount}");
                errors++;
                continue;
            }

            ref var character = ref registry.Characters[index];

            if (character.ID != id)
            {
                Logging.Error(LogCategory.Testing,
                    $"[RegistryLookupTest] ID mismatch. LookupID={id}, BlobID={character.ID}, Index={index}");
                errors++;
            }
        }

        if (errors == 0)
            Logging.Info(LogCategory.Testing,"[RegistryLookupTest] Registry consistency is valid. Test passed.");

        state.Enabled = false;
    }
}
