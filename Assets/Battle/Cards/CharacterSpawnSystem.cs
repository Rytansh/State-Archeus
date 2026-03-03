using Unity.Entities;
using Unity.Collections;
using DBUS.Battle.Components.Combat;
using DBUS.Battle.Components.Requests;
public partial struct CharacterSpawnSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        EntityCommandBuffer ecb =
            new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, requestEntity)
                 in SystemAPI.Query<RefRO<SpawnCharacterRequest>>()
                             .WithEntityAccess())
        {
            Entity character = ecb.CreateEntity();

            ecb.AddComponent(character, new Character{Battle = request.ValueRO.Battle});
            ecb.AddComponent(character, new CharacterSlot{Value = request.ValueRO.Slot});
            ecb.AddComponent(character, new CharacterSlot{Value = request.ValueRO.Slot});
            ecb.AddComponent(character, new CurrentAttack{Value = request.ValueRO.Attack});
            ecb.AddComponent(character, new CurrentDefense{Value = request.ValueRO.Defense});
            ecb.AddComponent(character, new MaxHealth{Value = request.ValueRO.MaxHealth});
            ecb.AddComponent(character, new CurrentHealth{Value = request.ValueRO.MaxHealth});

            ecb.DestroyEntity(requestEntity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

