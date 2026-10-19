using Archeus.Battle.Presentation.State;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Archeus.Battle.Presentation
{
    public sealed class CharacterHealthPresenter
    {
        public bool TryPresentDamage(
            EntityManager entityManager,
            EntityQuery characterStateQuery,
            uint targetRuntimeID,
            float damage
        )
        {
            using NativeArray<Entity> entities = characterStateQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                CharacterPresentationState state =
                    entityManager.GetComponentData<CharacterPresentationState>(entity);

                if (state.RuntimeID != targetRuntimeID)
                {
                    continue;
                }

                if (!entityManager.HasComponent<CharacterVisibleState>(entity))
                {
                    Logging.Error(
                        LogCategory.Presentation,
                        $"[HEALTH PRESENTATION] "
                            + $"Character="
                            + $"{targetRuntimeID} "
                            + $"has no "
                            + $"CharacterVisibleState."
                    );

                    return false;
                }

                CharacterVisibleState visibleState =
                    entityManager.GetComponentData<CharacterVisibleState>(entity);

                float before = visibleState.Health;

                visibleState.Health = math.max(0f, visibleState.Health - damage);

                entityManager.SetComponentData(entity, visibleState);

                Logging.Info(
                    LogCategory.Presentation,
                    $"[VISIBLE HEALTH] "
                        + $"Character={targetRuntimeID} | "
                        + $"Before={before} | "
                        + $"Damage={damage} | "
                        + $"After={visibleState.Health} | "
                        + $"Authoritative="
                        + $"{state.CurrentHealth}"
                );

                return true;
            }

            Logging.Warn(
                LogCategory.Presentation,
                $"[HEALTH PRESENTATION] "
                    + $"No presentation-state entity "
                    + $"found for Character="
                    + $"{targetRuntimeID}."
            );

            return false;
        }
    }
}
