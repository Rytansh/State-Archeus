using Archeus.Battle.Data.Actions;
using Unity.Entities;

namespace Archeus.Battle.Scheduling
{
    public struct ActionSchedulerCounters : IComponentData
    {
        public uint NextQueueActionID;
        public ulong NextControlID;
        public uint NextSelectionID;
    }

    public struct ActionSchedulerState : IComponentData
    {
        public SchedulerFailure Failure;
        public int DispatchesThisTurn;
        public ulong RetiredControlID;
        public int ControlsThisTurn;
    }

    public struct ActionEligibility : IComponentData
    {
        public uint ManualMask;
        public uint AutomaticMask;

        public static uint Mask(CharacterActionType type) => 1u << (int)type;
    }

    /// <summary>Only these bindings permit content-selected VM programs; inputs cannot select arbitrary code.</summary>
    public struct ActionProgramBinding : IBufferElementData
    {
        public CharacterActionType Type;
        public uint DefinitionID;
        public int ProgramIndex;
        public ActionTargetMode TargetMode;
        public int EnergyCost;
    }

    public struct ActionTargetHistory : IBufferElementData
    {
        public Entity Source;
        public Entity Target;
    }

    public struct ActionOutcomeRecord : IBufferElementData
    {
        public uint QueueActionID;
        public uint ExecutionID;
        public uint ContextID;
        public ActionOutcome Outcome;
        public uint CausingExecutionID;
    }

    public struct ActionControlRequest : IBufferElementData
    {
        public ulong RequestID;
        public uint ContextID;
        public uint ExecutionID;
        public uint FrameID;
        public int Priority;
        public uint OwnerRuntimeID;
        public int RegistrationIndex;
        public int TriggerIndex;
        public ActionControlKind Kind;
        public uint TargetQueueActionID;
        public uint RelativeQueueActionID;
        public int Position;
        public PlannedAction Action;
        public Entity TargetPlayer;
        public BattleStageMask Stages;
    }

    public struct ArmedActionControl : IBufferElementData
    {
        public ActionControlRequest Request;
    }

    public struct ActionControlResult : IBufferElementData
    {
        public ulong RequestID;
        public bool Accepted;
    }

    public struct PendingContextTransition : IBufferElementData
    {
        public ulong RequestID;
        public Entity Parent;
        public Entity Player;
        public Entity Character;
        public AttackingStageExecutionType Type;
    }

    public struct PendingStageSkip : IBufferElementData
    {
        public Entity Player;
        public uint ContextID;
        public BattleStageMask Stages;
    }

    public struct PendingInterruptSelection : IBufferElementData
    {
        public uint SelectionID;
        public uint ContextID;
        public Entity Player;
        public uint PhaseEpoch;
    }
}
