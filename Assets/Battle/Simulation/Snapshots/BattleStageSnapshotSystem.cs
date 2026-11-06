using System;
using System.Collections.Generic;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Scheduling;
using Archeus.Battle.Systems.Presentation;
using Archeus.Battle.Systems.Setup;
using Archeus.Core.Debugging;
using Archeus.Game.Bootstrap;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Snapshots
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleRootGroup))]
    [UpdateAfter(typeof(BattlePhaseTransitionSystem))]
    [UpdateBefore(typeof(PresentationFactExportSystem))]
    public partial class BattleStageSnapshotSystem : SystemBase
    {
        private sealed class Entry
        {
            public readonly StageSnapshotWindow Window = new();
            public BattleStateSnapshot Snapshot;
            public Entity Player;
            public bool CaptureFailed;
            public int FailedTurn;
            public ProvisionalStage FailedStage;
            public uint FailedContext;
            public uint FailedEpoch;
        }

        private readonly Dictionary<Entity, Entry> entries = new();
        private EntityQuery battles;

        protected override void OnCreate() =>
            battles = GetEntityQuery(
                ComponentType.ReadOnly<BattleTag>(),
                ComponentType.ReadOnly<BattleState>(),
                ComponentType.ReadOnly<BattleID>(),
                ComponentType.ReadOnly<ActiveTurnPlayer>(),
                ComponentType.ReadOnly<TurnCounter>()
            );

        protected override void OnDestroy() => entries.Clear();

        protected override void OnUpdate()
        {
            EntityManager.CompleteAllTrackedJobs();
            var removed = new List<Entity>();
            foreach (var pair in entries)
                if (
                    !EntityManager.Exists(pair.Key)
                    || !EntityManager.HasComponent<BattleTag>(pair.Key)
                )
                    removed.Add(pair.Key);
            foreach (Entity entity in removed)
                entries.Remove(entity);

            using NativeArray<Entity> all = battles.ToEntityArray(Allocator.Temp);
            foreach (Entity battle in all)
            {
                BattlePhase phase = EntityManager.GetComponentData<BattleState>(battle).Phase;
                if (!entries.TryGetValue(battle, out Entry entry))
                {
                    entry = new Entry();
                    entries.Add(battle, entry);
                }
                if (!TryStage(phase, out ProvisionalStage stage))
                {
                    Invalidate(battle, entry);
                    entry.CaptureFailed = false;
                    continue;
                }
                Entity player = EntityManager.GetComponentData<ActiveTurnPlayer>(battle).Player;
                int turn = EntityManager.GetComponentData<TurnCounter>(battle).CurrentTurn;
                ulong id = EntityManager.GetComponentData<BattleID>(battle).Value;
                if (
                    !EntityManager.Exists(player)
                    || !EntityManager.HasComponent<BattlePlayerIdentity>(player)
                )
                {
                    Invalidate(battle, entry);
                    continue;
                }
                ulong playerID = EntityManager
                    .GetComponentData<BattlePlayerIdentity>(player)
                    .PlayerID;
                uint contextID = CurrentContext(battle);
                uint epoch = CurrentEpoch(battle);
                if (
                    entry.Window.CanRestore(
                        entry.Window.Revision,
                        id,
                        playerID,
                        turn,
                        stage,
                        contextID,
                        epoch
                    )
                )
                    continue;
                Invalidate(battle, entry);
                if (HasPendingEntryWork(battle))
                    continue;
                // Fail closed once per entry rather than repeatedly accepting provisional inputs
                // without a usable baseline, or flooding the log with an unsupported schema.
                if (
                    entry.CaptureFailed
                    && entry.FailedTurn == turn
                    && entry.FailedStage == stage
                    && entry.FailedContext == contextID
                    && entry.FailedEpoch == epoch
                )
                    continue;
                try
                {
                    entry.Snapshot = BattleStateSnapshot.Capture(EntityManager, battle);
                    entry.Window.Open(id, playerID, turn, stage, contextID, epoch);
                    entry.Player = player;
                    SetStatus(battle, entry.Window.Revision, true);
                }
                catch (Exception exception)
                {
                    entry.CaptureFailed = true;
                    entry.FailedTurn = turn;
                    entry.FailedStage = stage;
                    entry.FailedContext = contextID;
                    entry.FailedEpoch = epoch;
                    Logging.Error(
                        LogCategory.Combat,
                        $"Stage snapshot capture failed; input disabled: {exception}"
                    );
                }
            }
        }

        private bool HasPendingEntryWork(Entity battle) =>
            (
                EntityManager.HasBuffer<Archeus.Battle.Buffers.Actions.ActionExecutionState>(battle)
                && EntityManager
                    .GetBuffer<Archeus.Battle.Buffers.Actions.ActionExecutionState>(battle)
                    .Length != 0
            )
            || (
                EntityManager.HasBuffer<ActionControlRequest>(battle)
                && EntityManager.GetBuffer<ActionControlRequest>(battle).Length != 0
            )
            || (
                EntityManager.HasBuffer<BattleEvent>(battle)
                && EntityManager.GetBuffer<BattleEvent>(battle).Length != 0
            )
            || (
                EntityManager.HasBuffer<ChainedBattleEvent>(battle)
                && EntityManager.GetBuffer<ChainedBattleEvent>(battle).Length != 0
            )
            || (
                EntityManager.HasBuffer<BehaviourExecutionRequest>(battle)
                && EntityManager.GetBuffer<BehaviourExecutionRequest>(battle).Length != 0
            );

        private void Invalidate(Entity battle, Entry entry)
        {
            entry.Window.Commit();
            entry.Snapshot = null;
            SetStatus(battle, entry.Window.Revision, false);
        }

        private void SetStatus(Entity battle, ulong revision, bool valid)
        {
            var status = new StageSnapshotStatus { Revision = revision, IsValid = valid };
            if (EntityManager.HasComponent<StageSnapshotStatus>(battle))
                EntityManager.SetComponentData(battle, status);
            else
                EntityManager.AddComponentData(battle, status);
        }

        public bool TryRevert(Entity player, ulong expectedRevision)
        {
            if (!EntityManager.Exists(player) || !EntityManager.HasComponent<OwnedBattle>(player))
                return false;
            Entity battle = EntityManager.GetComponentData<OwnedBattle>(player).Battle;
            if (
                !entries.TryGetValue(battle, out Entry entry)
                || entry.Snapshot == null
                || entry.Player != player
                || !EntityManager.Exists(battle)
            )
                return false;
            if (
                !TryStage(
                    EntityManager.GetComponentData<BattleState>(battle).Phase,
                    out ProvisionalStage stage
                )
            )
                return false;
            if (
                !entry.Window.CanRestore(
                    expectedRevision,
                    EntityManager.GetComponentData<BattleID>(battle).Value,
                    EntityManager.GetComponentData<BattlePlayerIdentity>(player).PlayerID,
                    EntityManager.GetComponentData<TurnCounter>(battle).CurrentTurn,
                    stage,
                    CurrentContext(battle),
                    CurrentEpoch(battle)
                )
            )
                return false;
            if (
                EntityManager.GetComponentData<ActiveTurnPlayer>(battle).Player != player
                || EntityManager.HasComponent<BattleDrawingCompleteTag>(battle)
                || EntityManager.HasComponent<BattlePlanningCompleteTag>(battle)
            )
                return false;

            try
            {
                entry.Snapshot.Restore(EntityManager);
            }
            catch
            {
                Invalidate(battle, entry);
                entry.CaptureFailed = true;
                entry.FailedTurn = entry.Window.Turn;
                entry.FailedStage = entry.Window.Stage;
                entry.FailedContext = entry.Window.ContextID;
                entry.FailedEpoch = entry.Window.PhaseEpoch;
                throw;
            }
            entry.Player = entry.Snapshot.ResolveRestoredEntity(entry.Player);
            entry.Window.Open(
                entry.Window.BattleID,
                entry.Window.PlayerID,
                entry.Window.Turn,
                entry.Window.Stage,
                entry.Window.ContextID,
                entry.Window.PhaseEpoch
            );
            SetStatus(battle, entry.Window.Revision, true);
            BattleStageRevertSystem.CancelBattleRequests(EntityManager, battle);
            if (EntityManager.HasBuffer<PresentationFact>(battle))
                EntityManager.GetBuffer<PresentationFact>(battle).Clear();
            World
                .GetExistingSystemManaged<PresentationUpdateExportSystem>()
                ?.ForgetBattle(entry.Window.BattleID);
            World
                .GetExistingSystemManaged<ActionOrderExportSystem>()
                ?.ForgetBattle(entry.Window.BattleID);
            // Operational publication counters stay monotonic; they are intentionally excluded
            // from the gameplay baseline, so the recovery snapshot cannot be rejected as stale.
            using var bridgeQuery = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePresentationBridgeReference>()
            );
            if (!bridgeQuery.IsEmptyIgnoreFilter)
            {
                var bridge = EntityManager
                    .GetComponentObject<BattlePresentationBridgeReference>(
                        bridgeQuery.GetSingletonEntity()
                    )
                    .Bridge;
                bridge.InvalidateProvisionalPresentation(entry.Window.BattleID);
            }
            return true;
        }

        private static bool TryStage(BattlePhase phase, out ProvisionalStage stage)
        {
            stage =
                phase == BattlePhase.Drawing ? ProvisionalStage.Drawing : ProvisionalStage.Planning;
            return phase == BattlePhase.Drawing || phase == BattlePhase.Planning;
        }

        private uint CurrentContext(Entity battle) =>
            ActionQueueService.TryActive(EntityManager, battle, out _, out var data)
                ? data.ContextID
                : 0;

        private uint CurrentEpoch(Entity battle) =>
            EntityManager.HasComponent<BattlePhaseEpoch>(battle)
                ? EntityManager.GetComponentData<BattlePhaseEpoch>(battle).Value
                : 0;
    }
}
