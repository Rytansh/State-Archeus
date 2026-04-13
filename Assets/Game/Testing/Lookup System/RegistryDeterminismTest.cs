using Archeus.Content.Lookup;
using Unity.Entities;
using Archeus.Core.Debugging;

[UpdateAfter(typeof(ContentLookupSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct RegistryDeterminismTest : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ContentLookupTables>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ref var lookups =
            ref SystemAPI.GetSingletonRW<ContentLookupTables>().ValueRW;

        // Pick a few canonical IDs
        uint[] criticalIds =
        {
            StableHash32.HashFromString("C1")
        };

        foreach (uint id in criticalIds)
        {
            if (lookups.CharacterIDToIndex.TryGetValue(id, out int index))
            {
                Logging.Info(LogCategory.Testing,$"ID={id} → Index={index}");
            }
            else
            {
                Logging.Error(LogCategory.Testing,$"[DeterminismTest] ID missing: {id}");
            }
        }

        state.Enabled = false;
    }
}

