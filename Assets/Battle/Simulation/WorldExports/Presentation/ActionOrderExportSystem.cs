using System.Collections.Generic;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup), OrderLast = true)]
    [UpdateAfter(typeof(PresentationUpdateExportSystem))]
    [UpdateBefore(typeof(PresentationStateRecoverySystem))]
    public partial class ActionOrderExportSystem : SystemBase
    {
        private readonly Dictionary<Entity, string> published = new();

        public void ForgetBattle(ulong battleID)
        {
            var stale = new List<Entity>();
            foreach (var battle in published.Keys)
                if (
                    !EntityManager.Exists(battle)
                    || EntityManager.GetComponentData<BattleID>(battle).Value == battleID
                )
                    stale.Add(battle);
            foreach (var battle in stale)
                published.Remove(battle);
        }

        protected override void OnUpdate()
        {
            using var bridges = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );
            if (bridges.IsEmptyIgnoreFilter)
                return;
            var bridge = EntityManager
                .GetComponentObject<BattlePresentationBridgeReference>(bridges.GetSingletonEntity())
                .Bridge;
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattleTag>(),
                ComponentType.ReadOnly<BattleID>(),
                ComponentType.ReadOnly<InitialPresentationStatePublishedTag>(),
                ComponentType.ReadWrite<PresentationStateRevision>()
            );
            using NativeArray<Entity> battles = query.ToEntityArray(Allocator.Temp);
            foreach (Entity battle in battles)
            {
                var projection = ActionOrderPublication.Capture(EntityManager, battle);
                string key = ActionOrderPublication.Fingerprint(projection);
                if (published.TryGetValue(battle, out string previous) && previous == key)
                    continue;
                var revision = EntityManager.GetComponentData<PresentationStateRevision>(battle);
                revision.Value++;
                EntityManager.SetComponentData(battle, revision);
                bridge.PublishActionOrderStateUpdate(
                    projection.BattleID,
                    revision.Value,
                    projection
                );
                published[battle] = key;
            }
            // Entity versions make keys safe against reuse, but bound cache lifetime as battles end.
            var stale = new List<Entity>();
            foreach (var battle in published.Keys)
                if (!EntityManager.Exists(battle))
                    stale.Add(battle);
            foreach (var battle in stale)
                published.Remove(battle);
        }
    }
}
