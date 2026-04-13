using UnityEngine;
using Unity.Entities;
using Archeus.Content.Lookup;
using Archeus.Core.Debugging;

[UpdateAfter(typeof(ContentLookupSystem))]
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct MissingRegistryTest : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ContentLookupTables>();
    }

    public void OnUpdate(ref SystemState state)
    {
        ref var lookups =
            ref SystemAPI.GetSingletonRW<ContentLookupTables>().ValueRW;

        uint invalidId = StableHash32.HashFromString("THIS_ID_SHOULD_NOT_EXIST");

        if (lookups.CharacterIDToIndex.TryGetValue(invalidId, out int index))
        {
            Logging.Error(LogCategory.Testing,$"Fail — Invalid ID resolved to index {index}");
        }
        else
        {
            Logging.Info(LogCategory.Testing,"Pass - Invalid ID correctly rejected.");
        }

        state.Enabled = false;
    }
}

