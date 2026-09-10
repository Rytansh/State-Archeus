using Archeus.Battle.Buffers.Presentation;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderFirst = true)]
    public partial class PresentationFactImportSystem : SystemBase
    {
        private EntityQuery bridgeQuery;
        private EntityQuery inboxQuery;

        protected override void OnCreate()
        {
            bridgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );

            inboxQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationInboxTag>(),
                ComponentType.ReadWrite<PresentationFact>()
            );

            RequireForUpdate(bridgeQuery);
            RequireForUpdate(inboxQuery);
        }

        protected override void OnUpdate()
        {
            Entity bridgeEntity = bridgeQuery.GetSingletonEntity();

            BattlePresentationBridgeReference bridgeReference =
                EntityManager.GetComponentObject<BattlePresentationBridgeReference>(bridgeEntity);

            BattlePresentationBridge bridge = bridgeReference.Bridge;

            Entity inboxEntity = inboxQuery.GetSingletonEntity();

            DynamicBuffer<PresentationFact> inbox = EntityManager.GetBuffer<PresentationFact>(
                inboxEntity
            );

            while (bridge.TryConsume(out PresentationFact fact))
            {
                inbox.Add(fact);

                Logging.Info(
                    LogCategory.Presentation,
                    $"Imported simulation fact: "
                        + $"Seq={fact.FactMetadata.Sequence} "
                        + $"Type={fact.FactType} "
                        + $"Target={fact.FactMetadata.TargetRuntimeID}"
                );
            }
        }
    }
}
