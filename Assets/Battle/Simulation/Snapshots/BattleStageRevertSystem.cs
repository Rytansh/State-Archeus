using System;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Systems.Input;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Snapshots
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInputGroup))]
    [UpdateAfter(typeof(BattleInputRoutingSystem))]
    public partial class BattleStageRevertSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var snapshots = World.GetExistingSystemManaged<BattleStageSnapshotSystem>();
            if (snapshots == null)
                return;
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RevertStageRequest>()
            );
            using NativeArray<Entity> requests = query.ToEntityArray(Allocator.Temp);
            foreach (Entity entity in requests)
            {
                if (!EntityManager.Exists(entity))
                    continue;
                RevertStageRequest request = EntityManager.GetComponentData<RevertStageRequest>(
                    entity
                );
                EntityManager.DestroyEntity(entity);
                if (
                    !EntityManager.Exists(request.Player)
                    || !EntityManager.HasComponent<OwnedBattle>(request.Player)
                )
                    continue;
                snapshots.TryRevert(request.Player, request.ExpectedRevision);
            }
            DiscardStaleRequests();
        }

        public static void CancelBattleRequests(EntityManager entities, Entity battle)
        {
            CancelOwned(entities, battle);
            Cancel<DrawCardsRequest>(entities, battle, value => value.Player);
            Cancel<PlaceCardRequest>(entities, battle, value => value.Player);
            Cancel<RetreatCardRequest>(entities, battle, value => value.Player);
            Cancel<EndDrawingRequest>(entities, battle, value => value.Player);
            Cancel<EndPlanningRequest>(entities, battle, value => value.Player);
            Cancel<PlayActionRequest>(entities, battle, value => value.Player);
            Cancel<CycleTargetRequest>(entities, battle, value => value.Player);
            Cancel<CycleCharacterRequest>(entities, battle, value => value.Player);
        }

        private static void CancelOwned(EntityManager entities, Entity battle)
        {
            using var query = entities.CreateEntityQuery(
                ComponentType.ReadOnly<StageCommandOwner>()
            );
            using NativeArray<Entity> requests = query.ToEntityArray(Allocator.Temp);
            foreach (Entity request in requests)
                if (entities.GetComponentData<StageCommandOwner>(request).Battle == battle)
                    entities.DestroyEntity(request);
        }

        private void DiscardStaleRequests()
        {
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<StageCommandOwner>()
            );
            using NativeArray<Entity> requests = query.ToEntityArray(Allocator.Temp);
            foreach (Entity request in requests)
            {
                var owner = EntityManager.GetComponentData<StageCommandOwner>(request);
                if (
                    !EntityManager.Exists(owner.Battle)
                    || !EntityManager.HasComponent<StageSnapshotStatus>(owner.Battle)
                )
                {
                    EntityManager.DestroyEntity(request);
                    continue;
                }
                var status = EntityManager.GetComponentData<StageSnapshotStatus>(owner.Battle);
                if (!status.IsValid || status.Revision != owner.Revision)
                    EntityManager.DestroyEntity(request);
            }
        }

        private static void Cancel<T>(
            EntityManager entities,
            Entity battle,
            Func<T, Entity> getPlayer
        )
            where T : unmanaged, IComponentData
        {
            using var query = entities.CreateEntityQuery(ComponentType.ReadOnly<T>());
            using NativeArray<Entity> requests = query.ToEntityArray(Allocator.Temp);
            foreach (Entity request in requests)
            {
                Entity player = getPlayer(entities.GetComponentData<T>(request));
                if (
                    !entities.Exists(player)
                    || (
                        entities.HasComponent<OwnedBattle>(player)
                        && entities.GetComponentData<OwnedBattle>(player).Battle == battle
                    )
                )
                    entities.DestroyEntity(request);
            }
        }
    }
}
