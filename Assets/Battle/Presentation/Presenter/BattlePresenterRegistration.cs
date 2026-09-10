using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Systems.Presentation;
using Archeus.Core.Debugging;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Battle.Presentation.Presenters
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattlePresentationGroup))]
    [UpdateBefore(typeof(PresentationPacketProbeSystem))]
    public partial class BattlePresenterRegistrationSystem : SystemBase
    {
        private EntityQuery registryQuery;
        private bool hasRegistered;

        protected override void OnCreate()
        {
            registryQuery = GetEntityQuery(ComponentType.ReadOnly<PresentationRegistryReference>());

            RequireForUpdate(registryQuery);
        }

        protected override void OnUpdate()
        {
            if (hasRegistered)
                return;

            Entity registryEntity = registryQuery.GetSingletonEntity();

            PresentationRegistryReference registryReference =
                EntityManager.GetComponentObject<PresentationRegistryReference>(registryEntity);

            PresentationRegistry registry = registryReference.Registry;

            CharacterPresenter[] presenters = Object.FindObjectsByType<CharacterPresenter>(
                FindObjectsSortMode.None
            );

            if (presenters.Length == 0)
            {
                // Battle scene/presenters probably aren't loaded yet.
                return;
            }

            Logging.Info(
                LogCategory.Presentation,
                $"[REGISTRY] Found {presenters.Length} CharacterPresenters."
            );

            for (int i = 0; i < presenters.Length; i++)
            {
                CharacterPresenter presenter = presenters[i];

                if (registry.Register(presenter))
                {
                    Logging.Info(
                        LogCategory.Presentation,
                        $"[REGISTRY] Registered "
                            + $"{presenter.gameObject.name} "
                            + $"as RuntimeID={presenter.RuntimeID}"
                    );
                }
                else
                {
                    Logging.Warn(
                        LogCategory.Presentation,
                        $"[REGISTRY] Failed to register "
                            + $"{presenter.gameObject.name} "
                            + $"with RuntimeID={presenter.RuntimeID}"
                    );
                }
            }

            hasRegistered = true;
        }
    }
}
