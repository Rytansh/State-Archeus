using System;
using System.Linq;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Effects;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Snapshots;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class BattleStateSnapshotTests
    {
        public struct NestedLinks
        {
            public Entity Target;
            public int Count;
        }

        public struct CustomState : IComponentData
        {
            public NestedLinks Links;
            public float Value;
        }

        public struct Switch : IComponentData, IEnableableComponent
        {
            public int Value;
        }

        public sealed class ManagedState : IComponentData
        {
            public string Value;
        }

        public struct NativeState : IComponentData
        {
            public IntPtr Value;
        }

        public struct ExtraTag : IComponentData { }

        private SnapshotTestFixture fixture;
        private EntityManager Entities => fixture.Entities;

        [SetUp]
        public void SetUp() => fixture = new SnapshotTestFixture();

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void RestoresHealthEnergyAPAndAddedRemovedComponents()
        {
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.SetComponentData(fixture.Card, new CurrentHealth { Value = 2 });
            Entities.SetComponentData(fixture.Card, new HeldSuperchargedEnergy { Value = 0 });
            Entities.SetComponentData(fixture.Player, new RemainingActionPoints { Value = 0 });
            Entities.RemoveComponent<Alive>(fixture.Card);
            Entities.AddComponent<ExtraTag>(fixture.Card);
            snapshot.Restore(Entities);
            Assert.That(
                Entities.GetComponentData<CurrentHealth>(fixture.Card).Value,
                Is.EqualTo(100)
            );
            Assert.That(
                Entities.GetComponentData<HeldSuperchargedEnergy>(fixture.Card).Value,
                Is.EqualTo(3)
            );
            Assert.That(
                Entities.GetComponentData<RemainingActionPoints>(fixture.Player).Value,
                Is.EqualTo(4)
            );
            Assert.That(Entities.HasComponent<Alive>(fixture.Card), Is.True);
            Assert.That(Entities.HasComponent<ExtraTag>(fixture.Card), Is.False);
            Assert.That(snapshot.ComputeCurrentHash(Entities), Is.EqualTo(snapshot.BaselineHash));
        }

        [Test]
        public void RestoresAllEffectKindsDurationsStacksAndPassiveMemory()
        {
            Entities
                .GetBuffer<ActiveEffect>(fixture.Card)
                .Add(
                    new ActiveEffect
                    {
                        EffectIndex = 7,
                        Source = fixture.Card,
                        RemainingDuration = 4,
                        Stacks = 3,
                        Strength = 0.25f,
                    }
                );
            Entities
                .GetBuffer<BasicEffectInstance>(fixture.Card)
                .Add(
                    new BasicEffectInstance
                    {
                        Type = BasicEffectType.DecreaseAttack,
                        TurnsRemaining = 2,
                        Stacks = 5,
                    }
                );
            Entities
                .GetBuffer<DOTEffectInstance>(fixture.Card)
                .Add(
                    new DOTEffectInstance
                    {
                        Type = DOTEffectType.Poison,
                        TurnsRemaining = 3,
                        Stacks = 2,
                        Multiplier = 1.25f,
                    }
                );
            var memory = new FixedList64Bytes<float>();
            memory.Add(1);
            memory.Add(2);
            memory.Add(99);
            Entities
                .GetBuffer<BehaviourRuntimeState>(fixture.Card)
                .Add(new BehaviourRuntimeState { Memory = memory });
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.GetBuffer<ActiveEffect>(fixture.Card).Clear();
            Entities.GetBuffer<BasicEffectInstance>(fixture.Card).Clear();
            Entities.GetBuffer<DOTEffectInstance>(fixture.Card).Clear();
            memory[2] = 100;
            Entities.GetBuffer<BehaviourRuntimeState>(fixture.Card)[0] = new BehaviourRuntimeState
            {
                Memory = memory,
            };
            Assert.That(
                snapshot.ComputeCurrentHash(Entities),
                Is.Not.EqualTo(snapshot.BaselineHash)
            );
            snapshot.Restore(Entities);
            Assert.That(snapshot.ComputeCurrentHash(Entities), Is.EqualTo(snapshot.BaselineHash));
            Assert.That(
                Entities.GetBuffer<BehaviourRuntimeState>(fixture.Card)[0].Memory[2],
                Is.EqualTo(99)
            );
        }

        [Test]
        public void RestoresBothRNGWordsAndIdenticalFutureOutcomes()
        {
            BattleRNG initial = Entities.GetComponentData<BattleRNG>(fixture.Battle);
            for (int i = 0; i < 13; i++)
                BattleRNGService.RollInt(ref initial, 1000);
            Entities.SetComponentData(fixture.Battle, initial);
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            int[] expected = new int[100];
            BattleRNG advanced = initial;
            for (int i = 0; i < expected.Length; i++)
                expected[i] = BattleRNGService.RollInt(ref advanced, 1000);
            Entities.SetComponentData(fixture.Battle, advanced);
            snapshot.Restore(Entities);
            BattleRNG restored = Entities.GetComponentData<BattleRNG>(fixture.Battle);
            Assert.That(restored.StateA, Is.EqualTo(initial.StateA));
            Assert.That(restored.StateB, Is.EqualTo(initial.StateB));
            for (int i = 0; i < expected.Length; i++)
                Assert.That(BattleRNGService.RollInt(ref restored, 1000), Is.EqualTo(expected[i]));
            Assert.That(restored.StateA, Is.EqualTo(advanced.StateA));
            Assert.That(restored.StateB, Is.EqualTo(advanced.StateB));
        }

        [Test]
        public void RestoresPendingEventsBehaviourRequestsAndActionQueues()
        {
            Entities
                .GetBuffer<BattleEvent>(fixture.Battle)
                .Add(new BattleEvent { Source = fixture.Card, Target = fixture.Card });
            Entities
                .GetBuffer<ChainedBattleEvent>(fixture.Battle)
                .Add(new ChainedBattleEvent { Event = new BattleEvent { Source = fixture.Card } });
            Entities
                .GetBuffer<BehaviourExecutionRequest>(fixture.Battle)
                .Add(
                    new BehaviourExecutionRequest
                    {
                        Owner = fixture.Card,
                        Source = fixture.Card,
                        Target = fixture.Card,
                        Priority = 3,
                    }
                );
            Entities
                .GetBuffer<ActionExecutionRequest>(fixture.Battle)
                .Add(
                    new ActionExecutionRequest
                    {
                        Source = fixture.Card,
                        PrimaryTarget = fixture.Card,
                    }
                );
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.GetBuffer<BattleEvent>(fixture.Battle).Clear();
            Entities.GetBuffer<ChainedBattleEvent>(fixture.Battle).Clear();
            Entities.GetBuffer<BehaviourExecutionRequest>(fixture.Battle).Clear();
            Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Clear();
            snapshot.Restore(Entities);
            Assert.That(snapshot.ComputeCurrentHash(Entities), Is.EqualTo(snapshot.BaselineHash));
        }

        [Test]
        public void RemovesProvisionalWorkAndDoesNotEmitGameplayEvents()
        {
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities
                .GetBuffer<BattleEvent>(fixture.Battle)
                .Add(new BattleEvent { Source = fixture.Card });
            Entities
                .GetBuffer<BehaviourExecutionRequest>(fixture.Battle)
                .Add(new BehaviourExecutionRequest { Owner = fixture.Card });
            Entities
                .GetBuffer<ActionExecutionRequest>(fixture.Battle)
                .Add(new ActionExecutionRequest { Source = fixture.Card });
            snapshot.Restore(Entities);
            Assert.That(Entities.GetBuffer<BattleEvent>(fixture.Battle).Length, Is.Zero);
            Assert.That(
                Entities.GetBuffer<BehaviourExecutionRequest>(fixture.Battle).Length,
                Is.Zero
            );
            Assert.That(Entities.GetBuffer<ActionExecutionRequest>(fixture.Battle).Length, Is.Zero);
        }

        [Test]
        public void RecreatesDestroyedEntitiesAndRemapsNestedAndBufferReferences()
        {
            Entities.AddComponentData(
                fixture.Player,
                new CustomState
                {
                    Links = new NestedLinks { Target = fixture.Card, Count = 9 },
                }
            );
            Entities
                .GetBuffer<ActiveEffect>(fixture.Card)
                .Add(new ActiveEffect { Source = fixture.Card });
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.DestroyEntity(fixture.Card);
            Entity provisional = fixture.CreateCard(999);
            snapshot.Restore(Entities);
            Entity restored = snapshot.ResolveRestoredEntity(fixture.Card);
            Assert.That(Entities.Exists(restored), Is.True);
            Assert.That(Entities.Exists(provisional), Is.False);
            Assert.That(Entities.GetBuffer<DeckCard>(fixture.Player)[0].Card, Is.EqualTo(restored));
            Assert.That(
                Entities.GetComponentData<CustomState>(fixture.Player).Links.Target,
                Is.EqualTo(restored)
            );
            Assert.That(Entities.GetBuffer<ActiveEffect>(restored)[0].Source, Is.EqualTo(restored));
            Assert.That(Entities.GetComponentData<CardRuntimeID>(restored).Value, Is.EqualTo(100));
            Assert.That(snapshot.ComputeCurrentHash(Entities), Is.EqualTo(snapshot.BaselineHash));
            Entities.DestroyEntity(restored);
            snapshot.Restore(Entities);
            Assert.That(snapshot.ComputeCurrentHash(Entities), Is.EqualTo(snapshot.BaselineHash));
        }

        [Test]
        public void RestoresEnableBitsAndDisabledEntities()
        {
            Entities.AddComponentData(fixture.Card, new Switch { Value = 5 });
            Entities.SetComponentEnabled<Switch>(fixture.Card, false);
            Entities.AddComponent<Disabled>(fixture.Card);
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.SetComponentEnabled<Switch>(fixture.Card, true);
            Entities.RemoveComponent<Disabled>(fixture.Card);
            snapshot.Restore(Entities);
            Assert.That(Entities.IsComponentEnabled<Switch>(fixture.Card), Is.False);
            Assert.That(Entities.HasComponent<Disabled>(fixture.Card), Is.True);
        }

        [Test]
        public void RepeatedRevertsAndIdenticalInputsProduceIdenticalFullState()
        {
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            string expected = null;
            for (int cycle = 0; cycle < 25; cycle++)
            {
                BattleRNG rng = Entities.GetComponentData<BattleRNG>(fixture.Battle);
                int damage = BattleRNGService.RollInt(ref rng, 1, 20);
                Entities.SetComponentData(fixture.Battle, rng);
                Entities.SetComponentData(fixture.Card, new CurrentHealth { Value = 100 - damage });
                Entities.SetComponentData(fixture.Player, new RemainingActionPoints { Value = 1 });
                string replay = snapshot.ComputeCurrentHash(Entities);
                if (expected == null)
                    expected = replay;
                Assert.That(replay, Is.EqualTo(expected));
                snapshot.Restore(Entities);
                Assert.That(
                    snapshot.ComputeCurrentHash(Entities),
                    Is.EqualTo(snapshot.BaselineHash)
                );
            }
        }

        [Test]
        public void UnsupportedManagedAndNativeStateFailsClosed()
        {
            Entities.AddComponentObject(fixture.Card, new ManagedState { Value = "not owned" });
            Assert.Throws<NotSupportedException>(() =>
                BattleStateSnapshot.Capture(Entities, fixture.Battle)
            );
            Entities.RemoveComponent<ManagedState>(fixture.Card);
            Entities.AddComponentData(fixture.Card, new NativeState { Value = new IntPtr(1) });
            Assert.Throws<NotSupportedException>(() =>
                BattleStateSnapshot.Capture(Entities, fixture.Battle)
            );
        }

        [Test]
        public void UnsupportedProvisionalAdditionFailsBeforeMutatingTheWorld()
        {
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.SetComponentData(fixture.Card, new CurrentHealth { Value = 17 });
            Entities.AddComponentObject(fixture.Card, new ManagedState());
            Assert.Throws<NotSupportedException>(() => snapshot.Restore(Entities));
            Assert.That(
                Entities.GetComponentData<CurrentHealth>(fixture.Card).Value,
                Is.EqualTo(17)
            );
        }

        [Test]
        public void AnotherBattleIsNotRewound()
        {
            Entity other = Entities.CreateEntity();
            Entities.AddComponentData(other, new BattleID { Value = 43 });
            Entity foreign = Entities.CreateEntity();
            Entities.AddComponentData(foreign, new OwnedBattle { Battle = other });
            Entities.AddComponentData(foreign, new CurrentHealth { Value = 19 });
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entities.SetComponentData(foreign, new CurrentHealth { Value = 7 });
            snapshot.Restore(Entities);
            Assert.That(Entities.GetComponentData<CurrentHealth>(foreign).Value, Is.EqualTo(7));
            Entities
                .GetBuffer<ActiveEffect>(fixture.Card)
                .Add(new ActiveEffect { Source = foreign });
            Assert.Throws<InvalidOperationException>(() =>
                BattleStateSnapshot.Capture(Entities, fixture.Battle)
            );
        }

        [Test]
        public void AotCatalogCoversAllGameAssemblyValueComponentsAndBuffers()
        {
            foreach (
                Type type in typeof(BattleState)
                    .Assembly.GetTypes()
                    .Where(type =>
                        type.IsValueType
                        && (
                            typeof(IComponentData).IsAssignableFrom(type)
                            || typeof(IBufferElementData).IsAssignableFrom(type)
                        )
                    )
            )
                Assert.That(SnapshotAotCatalog.IsRegistered(type), Is.True, type.FullName);
        }

        [Test]
        public void UnownedReferencedStateIsRejectedInsteadOfCapturingASharedSingleton()
        {
            Entity singleton = Entities.CreateEntity();
            Entities.AddComponentData(singleton, new CurrentHealth { Value = 73 });
            Entities.AddComponentData(
                fixture.Card,
                new CustomState { Links = new NestedLinks { Target = singleton } }
            );
            Assert.Throws<InvalidOperationException>(() =>
                BattleStateSnapshot.Capture(Entities, fixture.Battle)
            );
            Assert.That(Entities.GetComponentData<CurrentHealth>(singleton).Value, Is.EqualTo(73));
        }

        [Test]
        public void ProvisionalEscapeFromOwnershipFailsBeforeAnyRestorationMutation()
        {
            var snapshot = BattleStateSnapshot.Capture(Entities, fixture.Battle);
            Entity external = Entities.CreateEntity();
            Entities.AddComponentData(
                fixture.Card,
                new CustomState { Links = new NestedLinks { Target = external } }
            );
            Entities.SetComponentData(fixture.Card, new CurrentHealth { Value = 17 });
            Assert.Throws<InvalidOperationException>(() => snapshot.Restore(Entities));
            Assert.That(
                Entities.GetComponentData<CurrentHealth>(fixture.Card).Value,
                Is.EqualTo(17)
            );
            Assert.That(Entities.Exists(external), Is.True);
        }
    }
}
