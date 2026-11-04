using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.State;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PresentationFactImportSystem))]
    public partial class PresentationStageReversionSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            using var bridgeQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );
            if (bridgeQuery.IsEmptyIgnoreFilter)
                return;
            var bridge = EntityManager
                .GetComponentObject<BattlePresentationBridgeReference>(
                    bridgeQuery.GetSingletonEntity()
                )
                .Bridge;
            while (bridge.TryTakeReversion(out ulong battleID))
            {
                bool currentBattle = false;
                using (
                    var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<BattlePresentationState>()
                    )
                )
                using (NativeArray<Entity> states = query.ToEntityArray(Allocator.Temp))
                    foreach (Entity entity in states)
                        if (
                            EntityManager
                                .GetComponentData<BattlePresentationState>(entity)
                                .BattleRuntimeID == battleID
                        )
                            currentBattle = true;
                World
                    .GetExistingSystemManaged<TimelinePresentationExecutorSystem>()
                    ?.CancelBattle(battleID);
                using (
                    var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<BattlePresentationRunnerReference>()
                    )
                )
                using (NativeArray<Entity> runners = query.ToEntityArray(Allocator.Temp))
                    foreach (Entity entity in runners)
                    {
                        var runner = EntityManager
                            .GetComponentObject<BattlePresentationRunnerReference>(entity)
                            .Runner;
                        if (
                            runner != null
                            && (
                                (
                                    runner.ActivePlan != null
                                    && runner.ActivePlan.BattleRuntimeID == battleID
                                ) || (runner.ActivePlan == null && currentBattle)
                            )
                        )
                            runner.CancelPlayback();
                    }
                if (currentBattle)
                {
                    using var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PresentationRegistryReference>()
                    );
                    using NativeArray<Entity> registries = query.ToEntityArray(Allocator.Temp);
                    foreach (Entity entity in registries)
                        EntityManager
                            .GetComponentObject<PresentationRegistryReference>(entity)
                            .Registry.ResetAnimations();
                }
                using (
                    var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadWrite<PresentationFact>()
                    )
                )
                using (NativeArray<Entity> inboxes = query.ToEntityArray(Allocator.Temp))
                    foreach (Entity inbox in inboxes)
                    {
                        var facts = EntityManager.GetBuffer<PresentationFact>(inbox);
                        for (int i = facts.Length - 1; i >= 0; i--)
                            if (facts[i].FactMetadata.BattleRuntimeID == battleID)
                                facts.RemoveAt(i);
                    }
                using (
                    var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PresentationActionPacket>()
                    )
                )
                using (NativeArray<Entity> packets = query.ToEntityArray(Allocator.Temp))
                    foreach (Entity packet in packets)
                        if (
                            EntityManager
                                .GetComponentData<PresentationActionPacket>(packet)
                                .BattleRuntimeID == battleID
                        )
                            EntityManager.DestroyEntity(packet);
                using (
                    var query = EntityManager.CreateEntityQuery(
                        ComponentType.ReadOnly<PresentationExecutionPlanReference>()
                    )
                )
                using (NativeArray<Entity> plans = query.ToEntityArray(Allocator.Temp))
                    foreach (Entity plan in plans)
                        if (
                            EntityManager
                                .GetComponentObject<PresentationExecutionPlanReference>(plan)
                                .Plan.BattleRuntimeID == battleID
                        )
                            EntityManager.DestroyEntity(plan);
            }
        }
    }
}
