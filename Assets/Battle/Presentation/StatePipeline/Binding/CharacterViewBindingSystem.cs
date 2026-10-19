using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Presentation.State;
using Unity.Entities;

namespace Archeus.Battle.Systems.Presentation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup), OrderLast = true)]
    [UpdateAfter(typeof(TimelinePresentationExecutorSystem))]
    public partial class CharacterViewBindingSystem : SystemBase
    {
        private EntityQuery presentationRegistryQuery;

        private PresentationRegistry presentationRegistry;

        protected override void OnCreate()
        {
            presentationRegistryQuery = GetEntityQuery(
                ComponentType.ReadOnly<PresentationRegistryReference>()
            );

            RequireForUpdate(presentationRegistryQuery);
        }

        protected override void OnUpdate()
        {
            EnsurePresentationRegistry();

            if (presentationRegistry == null)
            {
                return;
            }

            foreach (
                var (projectedState, visibleState) in SystemAPI.Query<
                    RefRO<CharacterPresentationState>,
                    RefRO<CharacterVisibleState>
                >()
            )
            {
                uint runtimeID = projectedState.ValueRO.RuntimeID;

                if (!presentationRegistry.TryGet(runtimeID, out CharacterPresenter presenter))
                {
                    continue;
                }

                if (presenter.HealthBar == null)
                {
                    continue;
                }

                presenter.HealthBar.SetHealth(
                    visibleState.ValueRO.Health,
                    projectedState.ValueRO.MaxHealth
                );
            }
        }

        private void EnsurePresentationRegistry()
        {
            Entity registryEntity = presentationRegistryQuery.GetSingletonEntity();

            PresentationRegistryReference reference =
                EntityManager.GetComponentObject<PresentationRegistryReference>(registryEntity);

            presentationRegistry = reference.Registry;
        }
    }
}
