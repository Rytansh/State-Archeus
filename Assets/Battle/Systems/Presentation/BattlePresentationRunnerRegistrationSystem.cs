using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateBefore(typeof(TimelinePresentationExecutorSystem))]
    public partial class BattlePresentationRunnerRegistrationSystem : SystemBase
    {
        private EntityQuery runnerQuery;

        protected override void OnCreate()
        {
            runnerQuery = GetEntityQuery(
                ComponentType.ReadOnly<BattlePresentationRunnerReference>()
            );
        }

        protected override void OnUpdate()
        {
            if (!runnerQuery.IsEmptyIgnoreFilter)
                return;

            BattlePresentationRunner runner =
                Object.FindFirstObjectByType<BattlePresentationRunner>();

            if (runner == null)
                return;

            Entity entity = EntityManager.CreateEntity();

            EntityManager.SetName(entity, "Battle Presentation Runner Reference");

            EntityManager.AddComponentObject(
                entity,
                new BattlePresentationRunnerReference { Runner = runner }
            );
        }
    }
}
