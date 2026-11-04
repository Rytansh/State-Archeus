using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Systems.Presentation;
using Archeus.Game.Bootstrap;
using NUnit.Framework;
using Unity.Entities;

namespace Archeus.Testing.Battle.Snapshots
{
    public sealed class PresentationReversionTests
    {
        [Test]
        public void BridgeDropsOnlyAbandonedBattleAndRequestsRecovery()
        {
            var bridge = new BattlePresentationBridge();
            bridge.Publish(
                new PresentationFact
                {
                    FactMetadata = new PresentationFactMetadata { BattleRuntimeID = 42 },
                }
            );
            bridge.Publish(
                new PresentationFact
                {
                    FactMetadata = new PresentationFactMetadata { BattleRuntimeID = 43 },
                }
            );
            var state = new BattlePresentationState { BattleRuntimeID = 42 };
            bridge.PublishBattleStateUpdate(42, 1, state);
            state.BattleRuntimeID = 43;
            bridge.PublishBattleStateUpdate(43, 1, state);
            bridge.InvalidateProvisionalPresentation(42);
            Assert.That(bridge.PendingFactCount, Is.EqualTo(1));
            Assert.That(bridge.TryConsume(out var fact), Is.True);
            Assert.That(fact.FactMetadata.BattleRuntimeID, Is.EqualTo(43));
            Assert.That(bridge.TryConsumeStateUpdate(out var update), Is.True);
            Assert.That(update.BattleRuntimeID, Is.EqualTo(43));
            Assert.That(bridge.TryTakeReversion(out ulong reverted), Is.True);
            Assert.That(reverted, Is.EqualTo(42));
            Assert.That(bridge.TryTakeStateRecoveryRequest(out ulong recovery), Is.True);
            Assert.That(recovery, Is.EqualTo(42));
        }

        [Test]
        public void PresentationResetRemovesPacketsAndAlreadyImportedFacts()
        {
            using var world = new World("Presentation reset tests");
            var entities = world.EntityManager;
            var bridge = new BattlePresentationBridge();
            Entity reference = entities.CreateEntity();
            entities.AddComponentObject(
                reference,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );
            Entity inbox = entities.CreateEntity();
            entities
                .AddBuffer<PresentationFact>(inbox)
                .Add(
                    new PresentationFact
                    {
                        FactMetadata = new PresentationFactMetadata { BattleRuntimeID = 42 },
                    }
                );
            Entity abandoned = entities.CreateEntity();
            entities.AddComponentData(
                abandoned,
                new PresentationActionPacket { BattleRuntimeID = 42 }
            );
            Entity other = entities.CreateEntity();
            entities.AddComponentData(other, new PresentationActionPacket { BattleRuntimeID = 43 });
            bridge.InvalidateProvisionalPresentation(42);
            world.GetOrCreateSystemManaged<PresentationStageReversionSystem>().Update();
            Assert.That(entities.Exists(abandoned), Is.False);
            Assert.That(entities.Exists(other), Is.True);
            Assert.That(entities.GetBuffer<PresentationFact>(inbox).Length, Is.Zero);
        }

        [Test]
        public void PresentationAssemblyAcceptsReplayedActionIDsAfterInvalidation()
        {
            using var world = new World("Replayed presentation action tests");
            var entities = world.EntityManager;
            var bridge = new BattlePresentationBridge();
            Entity reference = entities.CreateEntity();
            entities.AddComponentObject(
                reference,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );
            Entity inbox = entities.CreateEntity();
            entities.AddComponent<BattlePresentationInboxTag>(inbox);
            entities.AddBuffer<PresentationFact>(inbox);
            var assembly = world.CreateSystem<BattlePresentationAssemblySystem>();
            entities
                .GetBuffer<PresentationFact>(inbox)
                .Add(
                    new PresentationFact
                    {
                        FactType = PresentationFactType.ActionStarted,
                        FactMetadata = new PresentationFactMetadata
                        {
                            BattleRuntimeID = 42,
                            ActionExecutionID = 1,
                            Sequence = 1,
                        },
                    }
                );
            assembly.Update(world.Unmanaged);
            bridge.InvalidateProvisionalPresentation(42);
            world.GetOrCreateSystemManaged<PresentationStageReversionSystem>().Update();
            entities
                .GetBuffer<PresentationFact>(inbox)
                .Add(
                    new PresentationFact
                    {
                        FactType = PresentationFactType.ActionStarted,
                        FactMetadata = new PresentationFactMetadata
                        {
                            BattleRuntimeID = 42,
                            ActionExecutionID = 1,
                            Sequence = 99,
                        },
                    }
                );
            assembly.Update(world.Unmanaged);
            using var packets = entities.CreateEntityQuery(
                ComponentType.ReadOnly<PresentationActionPacket>()
            );
            Assert.That(packets.CalculateEntityCount(), Is.EqualTo(1));
            Assert.That(
                entities
                    .GetComponentData<PresentationActionPacket>(packets.GetSingletonEntity())
                    .FirstSequence,
                Is.EqualTo(99)
            );
        }
    }
}
