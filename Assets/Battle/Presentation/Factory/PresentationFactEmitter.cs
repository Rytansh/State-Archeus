using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Presentation.Facts;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Presentation.Factory
{
    public static class PresentationFactEmitter
    {
        public static void EmitDamageAppliedFact(
            PresentationHitPayload hitPayload,
            PresentationFactContext context,
            DynamicBuffer<PresentationFact> factQueue,
            RefRW<PresentationSequenceCounter> sequenceCounter
        )
        {
            PresentationFact DamageAppliedFact = new PresentationFact
            {
                FactType = PresentationFactType.DamageApplied,
                FactMetadata = ConstructFactMetadata(context, sequenceCounter),
                FactPayload = new PresentationFactPayload { HitPayload = hitPayload },
            };

            EmitFinalFact(DamageAppliedFact, factQueue);
        }

        public static void EmitActionStartedFact(
            PresentationFactContext context,
            DynamicBuffer<PresentationFact> factQueue,
            RefRW<PresentationSequenceCounter> sequenceCounter
        )
        {
            PresentationFact ActionStartedFact = new PresentationFact
            {
                FactType = PresentationFactType.ActionStarted,
                FactMetadata = ConstructFactMetadata(context, sequenceCounter),
                FactPayload = default,
            };

            factQueue.Add(ActionStartedFact);
        }

        public static void EmitActionCompletedFact(
            PresentationFactContext context,
            DynamicBuffer<PresentationFact> factQueue,
            RefRW<PresentationSequenceCounter> sequenceCounter
        )
        {
            PresentationFact actionCompletedFact = new PresentationFact
            {
                FactType = PresentationFactType.ActionCompleted,
                FactMetadata = ConstructFactMetadata(context, sequenceCounter),
                FactPayload = default,
            };

            EmitFinalFact(actionCompletedFact, factQueue);
        }

        private static void EmitFinalFact(
            PresentationFact fact,
            DynamicBuffer<PresentationFact> factQueue
        )
        {
            factQueue.Add(fact);
            LogFact(in fact);
        }

        private static PresentationFactMetadata ConstructFactMetadata(
            PresentationFactContext context,
            RefRW<PresentationSequenceCounter> sequenceCounter
        )
        {
            return new PresentationFactMetadata
            {
                BattleRuntimeID = context.BattleRuntimeID,

                SourceRuntimeID = context.SourceRuntimeID,
                TargetRuntimeID = context.TargetRuntimeID,

                Sequence = RetrieveNextSequence(sequenceCounter),

                ActionDefinitionID = context.ActionDefinitionID,
                ActionExecutionID = context.ActionExecutionID,
                ActionResultIndex = context.ActionResultIndex,
                HitIndex = context.HitIndex,

                GroupID = context.GroupID,
                Generation = context.Generation,
            };
        }

        private static uint RetrieveNextSequence(RefRW<PresentationSequenceCounter> sequenceCounter)
        {
            uint nextSequence = sequenceCounter.ValueRO.NextSequence;

            sequenceCounter.ValueRW.NextSequence++;

            return nextSequence;
        }

        private static void LogFact(in PresentationFact fact)
        {
            PresentationFactMetadata metadata = fact.FactMetadata;

            switch (fact.FactType)
            {
                case PresentationFactType.DamageApplied:
                {
                    PresentationHitPayload hit = fact.FactPayload.HitPayload;

                    string actionResult =
                        metadata.ActionResultIndex == PresentationFactMetadata.NoActionResult
                            ? "None"
                            : metadata.ActionResultIndex.ToString();

                    string hitIndex =
                        metadata.HitIndex == PresentationFactMetadata.NoHit
                            ? "None"
                            : metadata.HitIndex.ToString();

                    Logging.Info(
                        LogCategory.Simulation,
                        $"Created presentation fact: "
                            + $"Seq={metadata.Sequence} | "
                            + $"Type={fact.FactType} | "
                            + $"Battle={metadata.BattleRuntimeID} | "
                            + $"Source={metadata.SourceRuntimeID} | "
                            + $"Target={metadata.TargetRuntimeID} | "
                            + $"Group={metadata.GroupID} | "
                            + $"Gen={metadata.Generation} | "
                            + $"Action={metadata.ActionExecutionID} | "
                            + $"RuntimeResult={actionResult} | "
                            + $"Hit={hitIndex} | "
                            + $"Damage={hit.Damage} | "
                            + $"Crit={hit.IsCrit}"
                    );

                    break;
                }

                default:
                {
                    Logging.Info(
                        LogCategory.Simulation,
                        $"Created presentation fact: "
                            + $"Seq={metadata.Sequence} | "
                            + $"Type={fact.FactType} | "
                            + $"Battle={metadata.BattleRuntimeID} | "
                            + $"Source={metadata.SourceRuntimeID} | "
                            + $"Target={metadata.TargetRuntimeID} | "
                            + $"Group={metadata.GroupID} | "
                            + $"Gen={metadata.Generation}"
                    );

                    break;
                }
            }
        }
    }
}
