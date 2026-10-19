using Archeus.Battle.Buffers.Presentation;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup), OrderLast = true)]
    public partial class PresentationFactExportSystem : SystemBase
    {
        private EntityQuery bridgeQuery;

        protected override void OnCreate()
        {
            bridgeQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );

            RequireForUpdate(bridgeQuery);
        }

        protected override void OnUpdate()
        {
            Entity bridgeEntity = bridgeQuery.GetSingletonEntity();

            BattlePresentationBridgeReference bridgeReference =
                EntityManager.GetComponentObject<BattlePresentationBridgeReference>(bridgeEntity);

            BattlePresentationBridge bridge = bridgeReference.Bridge;

            foreach (
                DynamicBuffer<PresentationFact> factBuffer in SystemAPI.Query<
                    DynamicBuffer<PresentationFact>
                >()
            )
            {
                for (int i = 0; i < factBuffer.Length; i++)
                {
                    PresentationFact fact = factBuffer[i];

                    bridge.Publish(fact);
                }

                factBuffer.Clear();
            }
        }
    }
}
