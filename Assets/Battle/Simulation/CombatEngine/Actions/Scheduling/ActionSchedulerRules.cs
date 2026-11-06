using System;

namespace Archeus.Battle.Scheduling
{
    public enum ActionOrigin : byte
    {
        PlayerPlanned,
        Passive,
        Counterattack,
        FollowUp,
        InterruptReplacement,
    }

    public enum ActionOutcome : byte
    {
        AttemptStarted,
        Authorized,
        ConsequencesApplied,
        Completed,
        Interrupted,
        Cancelled,
        Failed,
        Queued,
    }

    public enum ActionTargetMode : byte
    {
        SingleEnemy,
        Self,
        AllEnemies,
        None,
        SingleAlly,
    }

    public enum ActionControlKind : byte
    {
        InsertNext,
        Append,
        InsertAt,
        InsertAfter,
        RemovePending,
        MovePending,
        ReplacePending,
        InterruptCurrent,
        CancelCurrent,
        ReplaceCurrent,
        ExtraAction,
        ExtraTurn,
        SkipTurn,
        SkipStages,
        OfferInterruptSelection,
    }

    [Flags]
    public enum BattleStageMask : byte
    {
        None = 0,
        TurnStart = 1,
        Drawing = 2,
        Planning = 4,
        Attacking = 8,
        TurnEnd = 16,
        EntireTurn = 31,
    }

    public enum SchedulerFailure : byte
    {
        None,
        DispatchLimit,
        ContextDepth,
        OperationLimit,
        IdentityExhausted,
        EventEngineUnsettled,
    }

    public interface IStableQueuedAction
    {
        uint StableID { get; }
    }

    public interface IActionQueueStorage<T>
        where T : struct, IStableQueuedAction
    {
        int Count { get; }
        T this[int index] { get; set; }
        void Add(T value);
        void RemoveAt(int index);
    }

    /// <summary>Used directly by the ECS buffer adapter; contains no Unity or presentation state.</summary>
    public static class ActionQueueAlgorithms
    {
        public const int MaximumPendingActions = 256;
        public const int MaximumDispatchesPerTurn = 512;
        public const int MaximumContextDepth = 16;
        public const int MaximumControlsPerBoundary = 256;

        public static int Find<T, TStorage>(ref TStorage queue, uint id)
            where T : struct, IStableQueuedAction
            where TStorage : struct, IActionQueueStorage<T>
        {
            if (id == 0)
                return -1;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].StableID == id)
                    return i;
            return -1;
        }

        public static bool Insert<T, TStorage>(ref TStorage queue, T action, int position)
            where T : struct, IStableQueuedAction
            where TStorage : struct, IActionQueueStorage<T>
        {
            if (
                position < 0
                || position > queue.Count
                || queue.Count >= MaximumPendingActions
                || action.StableID == 0
                || Find<T, TStorage>(ref queue, action.StableID) >= 0
            )
                return false;
            queue.Add(action);
            for (int i = queue.Count - 1; i > position; i--)
                queue[i] = queue[i - 1];
            queue[position] = action;
            return true;
        }

        public static bool Remove<T, TStorage>(ref TStorage queue, uint id, out T removed)
            where T : struct, IStableQueuedAction
            where TStorage : struct, IActionQueueStorage<T>
        {
            int index = Find<T, TStorage>(ref queue, id);
            removed = default;
            if (index < 0)
                return false;
            removed = queue[index];
            queue.RemoveAt(index);
            return true;
        }

        public static bool Move<T, TStorage>(ref TStorage queue, uint id, int position)
            where T : struct, IStableQueuedAction
            where TStorage : struct, IActionQueueStorage<T>
        {
            int old = Find<T, TStorage>(ref queue, id);
            if (old < 0 || position < 0 || position >= queue.Count)
                return false;
            T value = queue[old];
            if (old < position)
                for (int i = old; i < position; i++)
                    queue[i] = queue[i + 1];
            else
                for (int i = old; i > position; i--)
                    queue[i] = queue[i - 1];
            queue[position] = value;
            return true;
        }

        public static bool CanInterrupt(ActionOutcome outcome) =>
            outcome == ActionOutcome.AttemptStarted;

        public static int CompareControl(
            int priorityA,
            uint ownerA,
            int registrationA,
            int triggerA,
            ulong sequenceA,
            int priorityB,
            uint ownerB,
            int registrationB,
            int triggerB,
            ulong sequenceB
        )
        {
            int value = priorityB.CompareTo(priorityA);
            if (value == 0)
                value = ownerA.CompareTo(ownerB);
            if (value == 0)
                value = registrationA.CompareTo(registrationB);
            if (value == 0)
                value = triggerA.CompareTo(triggerB);
            return value != 0 ? value : sequenceA.CompareTo(sequenceB);
        }
    }
}
