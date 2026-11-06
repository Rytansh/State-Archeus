using System;

namespace Archeus.Battle.Snapshots
{
    public enum ProvisionalStage : byte
    {
        Drawing,
        Planning,
    }

    /// <summary>Pure deterministic lifecycle; deliberately independent of ECS and presentation.</summary>
    public sealed class StageSnapshotWindow
    {
        public ulong Revision { get; private set; }
        public ulong BattleID { get; private set; }
        public ulong PlayerID { get; private set; }
        public int Turn { get; private set; }
        public ProvisionalStage Stage { get; private set; }
        public bool IsValid { get; private set; }
        public uint ContextID { get; private set; }
        public uint PhaseEpoch { get; private set; }

        public void Open(
            ulong battleID,
            ulong playerID,
            int turn,
            ProvisionalStage stage,
            uint contextID = 0,
            uint phaseEpoch = 0
        )
        {
            if (Revision == ulong.MaxValue)
                throw new InvalidOperationException("Snapshot revision exhausted.");
            Revision++;
            BattleID = battleID;
            PlayerID = playerID;
            Turn = turn;
            Stage = stage;
            ContextID = contextID;
            PhaseEpoch = phaseEpoch;
            IsValid = true;
        }

        public bool CanRestore(
            ulong revision,
            ulong battleID,
            ulong playerID,
            int turn,
            ProvisionalStage stage,
            uint contextID = 0,
            uint phaseEpoch = 0
        )
        {
            return IsValid
                && revision == Revision
                && battleID == BattleID
                && playerID == PlayerID
                && turn == Turn
                && stage == Stage
                && contextID == ContextID
                && phaseEpoch == PhaseEpoch;
        }

        public void Commit() => IsValid = false;
    }
}
