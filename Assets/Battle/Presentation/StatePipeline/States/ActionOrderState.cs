using System;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Scheduling;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public enum SchedulerFactKind : byte
    {
        Enqueued,
        Inserted,
        Removed,
        Moved,
        Dequeued,
        AttemptStarted,
        Authorized,
        Completed,
        Interrupted,
        Cancelled,
        Failed,
        ContextSuspended,
        ContextActivated,
        ContextResumed,
        ContextCompleted,
        StageSkipped,
        AttemptSettled,
    }

    public struct SchedulerFactPayload
    {
        public SchedulerFactKind Kind;
        public uint ContextID;
        public uint ParentContextID;
        public uint QueueActionID;
        public uint ExecutionID;
        public uint CausingExecutionID;
        public uint SourceRuntimeID;
        public uint TargetRuntimeID;
        public uint InheritedSourceRuntimeID;
        public CharacterActionType ActionType;
        public ActionOrigin Origin;
        public ActionOutcome Outcome;
        public AttackingStageExecutionType ContextType;
    }

    public struct ActionOrderItem
    {
        public uint QueueActionID;
        public uint ExecutionID;
        public uint ContextID;
        public uint SourceRuntimeID;
        public uint TargetRuntimeID;
        public uint CausingExecutionID;
        public uint InheritedSourceRuntimeID;
        public uint DefinitionID;
        public CharacterActionType ActionType;
        public ActionOrigin Origin;
        public ActionOutcome Outcome;
        public ActionTargetMode TargetMode;
    }

    public sealed class ActionOrderContextView
    {
        public uint ContextID;
        public uint ParentID;
        public ulong PlayerID;
        public uint RestrictedActor;
        public int RemainingAP;
        public AttackingStageExecutionType Type;
        public AttackingStageExecutionStatus Status;
        public ActionOrderItem[] Pending = Array.Empty<ActionOrderItem>();
    }

    public struct InterruptSelectionView
    {
        public uint SelectionID;
        public uint ContextID;
        public ulong PlayerID;
        public Archeus.Battle.Components.Ownership.BattleSide Side;
    }

    public struct ArmedControlView
    {
        public uint ContextID;
        public ActionControlKind Kind;
        public uint ActorRuntimeID;
        public uint TargetRuntimeID;
    }

    public sealed class ActionOrderSnapshot
    {
        public ulong BattleID;
        public uint PhaseEpoch;
        public uint ActiveContextID;
        public bool HasExecuting;
        public ActionOrderItem Executing;
        public SchedulerFailure Failure;
        public ActionOrderContextView[] Contexts = Array.Empty<ActionOrderContextView>();
        public uint[] Actors = Array.Empty<uint>();
        public uint[] Targets = Array.Empty<uint>();
        public InterruptSelectionView[] Selections = Array.Empty<InterruptSelectionView>();
        public ArmedControlView[] ArmedControls = Array.Empty<ArmedControlView>();
    }

    public sealed class ActionOrderStateUpdate : PresentationStateUpdate
    {
        public ActionOrderSnapshot State { get; }

        public ActionOrderStateUpdate(ulong battleID, ulong revision, ActionOrderSnapshot state)
            : base(battleID, revision) => State = state;
    }

    /// <summary>Presentation-world-only mirror, never a scheduler or gameplay authority.</summary>
    public sealed class ActionOrderMirror : IComponentData
    {
        public ActionOrderSnapshot State;
        public readonly System.Collections.Generic.Queue<SchedulerFactPayload> Transitions = new();

        public void Record(SchedulerFactPayload fact)
        {
            if (Transitions.Count >= 64)
                Transitions.Dequeue();
            Transitions.Enqueue(fact);
        }
    }
}
