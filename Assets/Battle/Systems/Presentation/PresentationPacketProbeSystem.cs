using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Presentation.Generic;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateAfter(typeof(BattlePresentationAssemblySystem))]
    public partial class PresentationPacketProbeSystem : SystemBase
    {
        private EntityQuery registryQuery;

        private GenericDamagePresenter genericDamagePresenter;

        protected override void OnCreate()
        {
            registryQuery = GetEntityQuery(ComponentType.ReadOnly<PresentationRegistryReference>());

            RequireForUpdate(registryQuery);

            genericDamagePresenter = new GenericDamagePresenter();
        }

        protected override void OnUpdate()
        {
            Entity registryEntity = registryQuery.GetSingletonEntity();

            PresentationRegistryReference registryReference =
                EntityManager.GetComponentObject<PresentationRegistryReference>(registryEntity);

            PresentationRegistry registry = registryReference.Registry;

            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (packet, actions, packetEntity) in SystemAPI
                    .Query<RefRO<PresentationActionPacket>, DynamicBuffer<PresentationAction>>()
                    .WithNone<PresentationPacketPresentedTag>()
                    .WithEntityAccess()
            )
            {
                if (packet.ValueRO.Status != PresentationActionPacketStatus.Sealed)
                {
                    continue;
                }

                bool successfullyPresented = TryPresentPacket(registry, packet.ValueRO, actions);

                if (successfullyPresented)
                {
                    ecb.AddComponent<PresentationPacketPresentedTag>(packetEntity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private bool TryPresentPacket(
            PresentationRegistry registry,
            in PresentationActionPacket packet,
            DynamicBuffer<PresentationAction> actions
        )
        {
            Logging.Info(
                LogCategory.Presentation,
                $"[PRESENTATION] Processing sealed packet | "
                    + $"Battle={packet.BattleRuntimeID} | "
                    + $"Action={packet.ActionExecutionID} | "
                    + $"Facts={actions.Length}"
            );

            // Make sure the action source exists.
            if (!registry.TryGet(packet.SourceRuntimeID, out CharacterPresenter sourcePresenter))
            {
                Logging.Warn(
                    LogCategory.Presentation,
                    $"[PRESENTATION] Could not resolve source " + $"{packet.SourceRuntimeID}"
                );

                return false;
            }

            Logging.Info(
                LogCategory.Presentation,
                $"[PRESENTATION] Source "
                    + $"{packet.SourceRuntimeID} "
                    + $"resolved to "
                    + $"{sourcePresenter.gameObject.name}"
            );

            /*
             * First pass:
             *
             * Validate that all presenters required by this
             * packet are available BEFORE presenting anything.
             *
             * This prevents us presenting half an Action,
             * discovering a missing target, then repeating the
             * first half again next frame.
             */
            for (int i = 0; i < actions.Length; i++)
            {
                PresentationFact fact = actions[i].Fact;

                if (fact.FactType != PresentationFactType.DamageApplied)
                {
                    continue;
                }

                uint targetRuntimeID = fact.FactMetadata.TargetRuntimeID;

                if (!registry.TryGet(targetRuntimeID, out _))
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[PRESENTATION] Could not resolve target "
                            + $"{targetRuntimeID} | "
                            + $"Seq={fact.FactMetadata.Sequence}"
                    );

                    return false;
                }
            }

            /*
             * Second pass:
             *
             * We now know all required presenters exist,
             * so actually present the facts.
             */
            for (int i = 0; i < actions.Length; i++)
            {
                PresentationFact fact = actions[i].Fact;

                switch (fact.FactType)
                {
                    case PresentationFactType.DamageApplied:
                    {
                        uint targetRuntimeID = fact.FactMetadata.TargetRuntimeID;

                        registry.TryGet(targetRuntimeID, out CharacterPresenter targetPresenter);

                        genericDamagePresenter.Present(in fact, targetPresenter);

                        break;
                    }

                    default:
                    {
                        // Other fact presenters come later.
                        break;
                    }
                }
            }

            return true;
        }
    }
}
