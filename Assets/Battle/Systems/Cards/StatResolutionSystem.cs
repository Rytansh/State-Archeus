using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Archeus.Game.Stats;
using Archeus.Content.Lookup;
using Archeus.Buffers.Effects;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Stats;

[BurstCompile]
[UpdateInGroup(typeof(BattleSimulationGroup))]
public partial struct StatResolutionSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ContentLookupTables>();
        state.RequireForUpdate<BattleContentRegistry>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var lookup = SystemAPI.GetSingleton<ContentLookupTables>();
        var registryRef = SystemAPI.GetSingleton<BattleContentRegistry>().BattleRegistryReference;
        ref var registry = ref registryRef.Value;

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (baseStats, resolvedStats, effects, entity) in
                 SystemAPI.Query<
                     RefRO<CharacterStats>,
                     RefRW<ResolvedCharacterStats>,
                     DynamicBuffer<ActiveEffect>
                 >()
                 .WithAll<StatsDirtyTag>()
                 .WithEntityAccess())
        {
            // ----------------------------------------
            // 1. INIT ACCUMULATORS (stat-indexed)
            // ----------------------------------------

            int statCount = (int)StatType.Count;

            NativeArray<float> flat = new NativeArray<float>(statCount, Allocator.Temp);
            NativeArray<float> percentAdd = new NativeArray<float>(statCount, Allocator.Temp);
            NativeArray<float> percentMult = new NativeArray<float>(statCount, Allocator.Temp);

            // Initialize multiplicative layer to 1
            for (int i = 0; i < statCount; i++)
                percentMult[i] = 1f;

            // ----------------------------------------
            // 2. ACCUMULATE MODIFIERS
            // ----------------------------------------

            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                int effectIndex = effect.EffectIndex;

                ref var def = ref registry.Effects[effectIndex];

                for (int j = 0; j < def.Modifiers.Length; j++)
                {
                    var mod = def.Modifiers[j];

                    int statIndex = (int)mod.StatType;

                    switch (mod.ModifierType)
                    {
                        case StatModifierType.Flat:
                            flat[statIndex] += mod.Value;
                            break;

                        case StatModifierType.PercentAdd:
                            percentAdd[statIndex] += mod.Value;
                            break;

                        case StatModifierType.PercentMultiply:
                            percentMult[statIndex] *= mod.Value;
                            break;
                    }
                }
            }

            // ----------------------------------------
            // 3. APPLY PIPELINE (BASE → FINAL)
            // ----------------------------------------

            // Base values → array (avoids branching later)
            NativeArray<float> baseValues = new NativeArray<float>(statCount, Allocator.Temp);

            baseValues[(int)StatType.Attack] = baseStats.ValueRO.Attack;
            baseValues[(int)StatType.Defense] = baseStats.ValueRO.Defense;
            baseValues[(int)StatType.MaxHealth] = baseStats.ValueRO.MaxHealth;
            baseValues[(int)StatType.CritRATE] = baseStats.ValueRO.CritRATE;
            baseValues[(int)StatType.CritDMG] = baseStats.ValueRO.CritDMG;

            NativeArray<float> finalValues = new NativeArray<float>(statCount, Allocator.Temp);

            for (int i = 0; i < statCount; i++)
            {
                float value = baseValues[i];

                value += flat[i];
                value *= (1f + percentAdd[i]);
                value *= percentMult[i];

                finalValues[i] = value;
            }

            // ----------------------------------------
            // 4. WRITE BACK (explicit mapping)
            // ----------------------------------------

            var resolved = resolvedStats.ValueRW;

            resolved.Attack = finalValues[(int)StatType.Attack];
            resolved.Defense = finalValues[(int)StatType.Defense];
            resolved.MaxHealth = finalValues[(int)StatType.MaxHealth];
            resolved.CritRATE = finalValues[(int)StatType.CritRATE];
            resolved.CritDMG = finalValues[(int)StatType.CritDMG];

            // ----------------------------------------
            // 5. CLEANUP
            // ----------------------------------------

            flat.Dispose();
            percentAdd.Dispose();
            percentMult.Dispose();
            baseValues.Dispose();
            finalValues.Dispose();

            ecb.RemoveComponent<StatsDirtyTag>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}