using Archeus.Battle.Presentation.State;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Archeus.Battle.Presentation.Presenters
{
    public sealed class CharacterVisibleStateReconciliation
    {
        private const float HealthEpsilon = 0.001f;

        public int Reconcile(
            EntityManager entityManager,
            EntityQuery characterStateQuery,
            ulong actionExecutionID
        )
        {
            using NativeArray<Entity> entities = characterStateQuery.ToEntityArray(Allocator.Temp);

            int correctedCount = 0;

            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];

                CharacterPresentationState authoritative =
                    entityManager.GetComponentData<CharacterPresentationState>(entity);

                CharacterVisibleState visible =
                    entityManager.GetComponentData<CharacterVisibleState>(entity);

                bool healthMismatch =
                    math.abs(visible.Health - authoritative.CurrentHealth) > HealthEpsilon;

                bool aliveMismatch = visible.IsAlive != authoritative.IsAlive;

                if (!healthMismatch && !aliveMismatch)
                {
                    continue;
                }

                float previousHealth = visible.Health;

                bool previousAlive = visible.IsAlive;

                /*
                 * Reconciliation does not calculate gameplay.
                 *
                 * It copies already-authoritative projected
                 * state into the visible presentation state.
                 */
                visible.Health = authoritative.CurrentHealth;

                visible.IsAlive = authoritative.IsAlive;

                entityManager.SetComponentData(entity, visible);

                correctedCount++;

                Logging.Warn(
                    LogCategory.Presentation,
                    $"[VISIBLE STATE RECONCILE] "
                        + $"Action={actionExecutionID} | "
                        + $"Character="
                        + $"{authoritative.RuntimeID} | "
                        + $"Health="
                        + $"{previousHealth}"
                        + $" -> "
                        + $"{visible.Health} | "
                        + $"Alive="
                        + $"{previousAlive}"
                        + $" -> "
                        + $"{visible.IsAlive}"
                );
            }

            Logging.Info(
                LogCategory.Presentation,
                $"[VISIBLE STATE RECONCILE COMPLETE] "
                    + $"Action={actionExecutionID} | "
                    + $"Checked={entities.Length} | "
                    + $"Corrected={correctedCount}"
            );

            return correctedCount;
        }
    }
}
