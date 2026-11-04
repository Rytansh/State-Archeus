using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Effects;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Data.VM;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Effects;
using Archeus.Battle.Systems.Events;
using Archeus.Battle.VM.Execution;
using Archeus.Content.Registries;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class DeterministicEventReversionTests
    {
        [Test]
        public void PassiveTieBreakUsesRuntimeIdentityInsteadOfEntityAllocation()
        {
            var comparer = new BehaviourExecutionComparer();
            var a = new BehaviourExecutionRequest
            {
                Owner = new Entity { Index = 1, Version = 1 },
                OwnerRuntimeID = 200,
            };
            var b = new BehaviourExecutionRequest
            {
                Owner = new Entity { Index = 2, Version = 1 },
                OwnerRuntimeID = 100,
            };
            int before = comparer.Compare(a, b);
            a.Owner = new Entity { Index = 999, Version = 5 };
            b.Owner = new Entity { Index = 3, Version = 8 };
            Assert.That(comparer.Compare(a, b), Is.EqualTo(before));
            Assert.That(before, Is.GreaterThan(0));
        }

        [Test]
        public void EffectExpirationOrderSurvivesDestructionAndRecreation()
        {
            using var fixture = new SnapshotTestFixture();
            var entities = fixture.Entities;
            Entity second = fixture.CreateCard(101);
            entities
                .GetBuffer<ActiveEffect>(fixture.Card)
                .Add(new ActiveEffect { RemainingDuration = 1 });
            entities
                .GetBuffer<ActiveEffect>(second)
                .Add(new ActiveEffect { RemainingDuration = 1 });
            var snapshot = BattleStateSnapshot.Capture(entities, fixture.Battle);
            var duration = fixture.World.CreateSystem<EffectDurationProcessingSystem>();
            entities.AddComponent<EffectDurationsProcessingTag>(fixture.Battle);
            duration.Update(fixture.World.Unmanaged);
            Assert.That(entities.GetBuffer<BattleEvent>(fixture.Battle).Length, Is.EqualTo(2));
            Assert.That(
                entities
                    .GetComponentData<CardRuntimeID>(
                        entities.GetBuffer<BattleEvent>(fixture.Battle)[0].Target
                    )
                    .Value,
                Is.EqualTo(100)
            );
            entities.DestroyEntity(fixture.Card);
            snapshot.Restore(entities);
            entities.AddComponent<EffectDurationsProcessingTag>(fixture.Battle);
            duration.Update(fixture.World.Unmanaged);
            var events = entities.GetBuffer<BattleEvent>(fixture.Battle);
            Assert.That(events.Length, Is.EqualTo(2));
            Assert.That(
                entities.GetComponentData<CardRuntimeID>(events[0].Target).Value,
                Is.EqualTo(100)
            );
            Assert.That(
                entities.GetComponentData<CardRuntimeID>(events[1].Target).Value,
                Is.EqualTo(101)
            );
        }

        [Test]
        public void RealPassiveVMStateAndAppliedEffectRevertAndReplayExactly()
        {
            using var fixture = new SnapshotTestFixture();
            var entities = fixture.Entities;
            using var builder = new BlobBuilder(Allocator.Temp);
            ref ContentBlobRegistry root = ref builder.ConstructRoot<ContentBlobRegistry>();
            var behaviours = builder.Allocate(ref root.Behaviours, 1);
            var triggers = builder.Allocate(ref behaviours[0].Triggers, 1);
            triggers[0].EventType = BattleEventType.TurnStarted;
            triggers[0].Phase = BattleEventPhase.PostResolution;
            triggers[0].OwnerType = TriggerOwnerType.Any;
            triggers[0].VMProgramIndex = 0;
            builder.Allocate(ref triggers[0].Conditions, 0);
            var effects = builder.Allocate(ref root.Effects, 1);
            effects[0].StackBehaviour = StackingBehaviour.AddInstance;
            builder.Allocate(ref effects[0].StatModifiers, 0);
            builder.Allocate(ref effects[0].BehaviourIndices, 0);
            var programs = builder.Allocate(ref root.AbilityPrograms, 1);
            var code = builder.Allocate(ref programs[0].Instructions, 8);
            code[0] = new AbilityInstruction { Opcode = AbilityOpcode.LoadState, A = 0 };
            code[1] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(1),
            };
            code[2] = new AbilityInstruction { Opcode = AbilityOpcode.Add };
            code[3] = new AbilityInstruction { Opcode = AbilityOpcode.StoreState, A = 0 };
            code[4] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(2),
            };
            code[5] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.PushConst,
                A = VMEncoding.EncodeFloat(0.5f),
            };
            code[6] = new AbilityInstruction
            {
                Opcode = AbilityOpcode.ApplyEffect,
                A = 0,
                Flags = AbilityInstructionFlags.GameplayOperation,
            };
            code[7] = new AbilityInstruction { Opcode = AbilityOpcode.End };
            var content = builder.CreateBlobAssetReference<ContentBlobRegistry>(
                Allocator.Persistent
            );
            try
            {
                entities.AddComponentData(
                    fixture.Battle,
                    new BattleContentRegistry { BattleRegistryReference = content }
                );
                entities
                    .AddBuffer<BattleParticipant>(fixture.Battle)
                    .Add(new BattleParticipant { Participant = fixture.Card });
                entities
                    .AddBuffer<BehaviourReference>(fixture.Card)
                    .Add(new BehaviourReference { BehaviourIndex = 0 });
                entities.GetBuffer<BehaviourRuntimeState>(fixture.Card).Add(default);
                Entity otherBattle = entities.CreateEntity();
                entities.AddComponentData(otherBattle, new BattleID { Value = 43 });
                Entity foreignPassive = entities.CreateEntity();
                entities.AddComponentData(foreignPassive, new OwnedBattle { Battle = otherBattle });
                entities.AddComponentData(foreignPassive, new CardRuntimeID { Value = 201 });
                entities
                    .AddBuffer<BehaviourReference>(foreignPassive)
                    .Add(new BehaviourReference { BehaviourIndex = 0 });
                entities.AddBuffer<BehaviourRuntimeState>(foreignPassive).Add(default);
                var processing = fixture.World.CreateSystem<BattleEventProcessingSystem>();
                var snapshot = BattleStateSnapshot.Capture(entities, fixture.Battle);
                string expected = null;
                for (int replay = 0; replay < 2; replay++)
                {
                    entities
                        .GetBuffer<BattleEvent>(fixture.Battle)
                        .Add(
                            new BattleEvent
                            {
                                Type = BattleEventType.TurnStarted,
                                Scope = BattleEventScope.Global,
                                Source = fixture.Card,
                                Target = fixture.Card,
                            }
                        );
                    processing.Update(fixture.World.Unmanaged);
                    Assert.That(
                        entities.GetBuffer<BehaviourRuntimeState>(foreignPassive)[0].Memory.Length,
                        Is.Zero
                    );
                    Assert.That(
                        entities.GetBuffer<BehaviourRuntimeState>(fixture.Card)[0].Memory[0],
                        Is.EqualTo(1)
                    );
                    Assert.That(
                        entities.GetBuffer<ActiveEffect>(fixture.Card).Length,
                        Is.EqualTo(1)
                    );
                    Assert.That(
                        entities.GetBuffer<ActiveEffect>(fixture.Card)[0].RemainingDuration,
                        Is.EqualTo(2)
                    );
                    string actual = snapshot.ComputeCurrentHash(entities);
                    if (expected == null)
                        expected = actual;
                    Assert.That(actual, Is.EqualTo(expected));
                    snapshot.Restore(entities);
                    Assert.That(entities.GetBuffer<ActiveEffect>(fixture.Card).Length, Is.Zero);
                    Assert.That(
                        entities.GetBuffer<BehaviourRuntimeState>(fixture.Card)[0].Memory.Length,
                        Is.Zero
                    );
                    Assert.That(
                        snapshot.ComputeCurrentHash(entities),
                        Is.EqualTo(snapshot.BaselineHash)
                    );
                }
            }
            finally
            {
                entities.RemoveComponent<BattleContentRegistry>(fixture.Battle);
                content.Dispose();
            }
        }
    }
}
