using System;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Data.Actions;
using Archeus.Battle.Presentation.State;
using Archeus.Battle.Scheduling;
using Archeus.Game.Bootstrap;
using Archeus.Game.Input;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Battle.Presentation
{
    /// <summary>Development view over the presentation mirror. All gameplay commands use the input gateway.</summary>
    public sealed class ActionOrderDebugView : MonoBehaviour
    {
        private uint actor;
        private uint target;
        private bool show = true;
        private bool controls;
        private bool simpleHistory = true;
        private Vector2 scroll;

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (
                GUI.Button(
                    new Rect(460, 10, 210, 25),
                    show ? "Hide action order" : "Show action order"
                )
            )
                show = !show;
            if (
                !show
                || RunBootstrap.RootContext == null
                || !RunBootstrap.RootContext.TryResolve<BattlePresentationWorld>(
                    out var presentation
                )
                || !RunBootstrap.RootContext.TryResolve<BattleInputGateway>(out var gateway)
                || !presentation.EcsWorld.IsCreated
            )
                return;
            var em = presentation.EcsWorld.EntityManager;
            using var mirrorQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<ActionOrderMirror>()
            );
            using var battleQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<BattlePresentationState>()
            );
            if (mirrorQuery.IsEmptyIgnoreFilter || battleQuery.IsEmptyIgnoreFilter)
                return;
            var mirror = em.GetComponentObject<ActionOrderMirror>(mirrorQuery.GetSingletonEntity());
            var order = mirror.State;
            if (order == null)
                return;
            var battle = em.GetComponentData<BattlePresentationState>(
                battleQuery.GetSingletonEntity()
            );
            if (battle.BattleRuntimeID != order.BattleID)
                return;
            GUILayout.BeginArea(
                new Rect(
                    460,
                    40,
                    Math.Max(240, Math.Min(650, Screen.width - 470)),
                    Math.Max(150, Screen.height - 50)
                ),
                GUI.skin.box
            );
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(
                $"ACTION ORDER | Battle {order.BattleID} | {battle.Phase} | turn {battle.TurnNumber} | epoch {order.PhaseEpoch}"
            );
            if (order.Failure != SchedulerFailure.None)
                GUILayout.Label($"Scheduler halted: {order.Failure}");
            if (order.HasExecuting)
                GUILayout.Label("Executing " + Describe(order.Executing));
            foreach (var context in order.Contexts)
            {
                GUILayout.Label(
                    $"Context {context.ContextID} → parent {context.ParentID}: {context.Type}, {context.Status}, AP {context.RemainingAP}, actor restriction {context.RestrictedActor}"
                );
                for (int i = 0; i < context.Pending.Length; i++)
                {
                    var item = context.Pending[i];
                    GUILayout.Label($"  [{i}] {Describe(item)}");
                    GUILayout.BeginHorizontal();
                    if (
                        battle.Phase == BattlePhase.Planning
                        && context.ContextID == order.ActiveContextID
                    )
                    {
                        if (item.Origin == ActionOrigin.PlayerPlanned)
                        {
                            if (GUILayout.Button("Remove"))
                                gateway.RequestRemoveAction(
                                    order.BattleID,
                                    battle.ActiveSide,
                                    item.QueueActionID
                                );
                            if (i > 0 && GUILayout.Button("Move up"))
                                gateway.RequestMoveAction(
                                    order.BattleID,
                                    battle.ActiveSide,
                                    item.QueueActionID,
                                    i - 1
                                );
                            if (i + 1 < context.Pending.Length && GUILayout.Button("Move down"))
                                gateway.RequestMoveAction(
                                    order.BattleID,
                                    battle.ActiveSide,
                                    item.QueueActionID,
                                    i + 1
                                );
                        }
                        foreach (var offer in order.Selections)
                            if (
                                offer.ContextID == context.ContextID
                                && GUILayout.Button(
                                    $"{offer.Side}: interrupt #{item.QueueActionID}"
                                )
                            )
                                gateway.RequestSelectInterrupt(
                                    order.BattleID,
                                    offer.Side,
                                    offer.SelectionID,
                                    item.QueueActionID
                                );
                    }
                    GUILayout.EndHorizontal();
                }
            }
            if (order.Selections.Length > 0)
                GUILayout.Label(
                    "Enemy action selection pending: select a queue row above before Attacking can begin."
                );
            GUILayout.Label($"Selected actor {actor}, target {target}");
            GUILayout.BeginHorizontal();
            foreach (uint id in order.Actors)
                if (GUILayout.Button($"Actor {id}"))
                    actor = id;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (uint id in order.Targets)
                if (GUILayout.Button($"Target {id}"))
                    target = id;
            GUILayout.EndHorizontal();
            if (battle.Phase == BattlePhase.Planning)
            {
                if (GUILayout.Button("Plan Normal Attack"))
                    gateway.RequestQueueAction(order.BattleID, battle.ActiveSide, actor, target);
                if (GUILayout.Button("Plan Supercharged Attack"))
                    gateway.RequestQueueAction(
                        order.BattleID,
                        battle.ActiveSide,
                        actor,
                        target,
                        CharacterActionType.SuperchargedAttack
                    );
                if (GUILayout.Button("Commit Planning"))
                    gateway.RequestEndPlanning(order.BattleID, battle.ActiveSide);
                if (GUILayout.Button("Revert this Planning window"))
                    gateway.RequestRevertStage(order.BattleID, battle.ActiveSide);
            }
            if (
                GUILayout.Button(
                    controls ? "Hide development controls" : "Enable development controls"
                )
            )
            {
                controls = !controls;
                if (controls)
                    gateway.EnableSchedulerDebug(order.BattleID, battle.ActiveSide);
            }
            if (controls)
            {
                GUILayout.Label(
                    "Development requests are validated by simulation. Armed requests run at the next action attempt."
                );
                DebugButton(
                    "Insert next automatically",
                    ActionControlKind.InsertNext,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Append automatically",
                    ActionControlKind.Append,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Arm next attempt: interrupt",
                    ActionControlKind.InterruptCurrent,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Arm next attempt: replace with selected actor",
                    ActionControlKind.ReplaceCurrent,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Arm Extra Action after next action",
                    ActionControlKind.ExtraAction,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Arm Extra Turn after next action",
                    ActionControlKind.ExtraTurn,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Skip selected target owner's next normal turn",
                    ActionControlKind.SkipTurn,
                    order,
                    battle,
                    gateway
                );
                DebugButton(
                    "Offer enemy preselected interruption",
                    ActionControlKind.OfferInterruptSelection,
                    order,
                    battle,
                    gateway
                );
            }
            GUILayout.Space(8);
            GUILayout.Label($"Armed controls ({order.ArmedControls.Length}):");
            if (order.ArmedControls.Length == 0)
                GUILayout.Label("  None");
            foreach (var armed in order.ArmedControls)
                GUILayout.Label(
                    $"  NEXT ATTEMPT | {armed.Kind} | actor {armed.ActorRuntimeID}, target {armed.TargetRuntimeID} | context {armed.ContextID}"
                );
            GUILayout.Space(8);
            GUILayout.Label(simpleHistory ? "Execution history (simple):" : "Scheduler transitions (detailed, latest 64):");
            if (GUILayout.Button(simpleHistory ? "Show detailed transitions" : "Show simple execution history"))
                simpleHistory = !simpleHistory;
            foreach (var fact in mirror.Transitions)
            {
                string line = simpleHistory ? SimplifyTransition(fact) : DescribeTransition(fact);
                if (!string.IsNullOrEmpty(line))
                    GUILayout.Label(line);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void DebugButton(
            string label,
            ActionControlKind kind,
            ActionOrderSnapshot state,
            BattlePresentationState battle,
            BattleInputGateway gateway
        )
        {
            if (GUILayout.Button(label))
                gateway.RequestDebugSchedulerControl(
                    state.BattleID,
                    battle.ActiveSide,
                    kind,
                    actor,
                    target
                );
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string SimplifyTransition(SchedulerFactPayload fact)
        {
            switch (fact.Kind)
            {
                case SchedulerFactKind.Enqueued:
                    return $"Queued: actor {fact.SourceRuntimeID} - {fact.ActionType} (#{fact.QueueActionID})";
                case SchedulerFactKind.Inserted:
                    return fact.Origin == ActionOrigin.InterruptReplacement
                        ? $"Replacement inserted: actor {fact.SourceRuntimeID} - {fact.ActionType} (#{fact.QueueActionID}, caused by execution {fact.CausingExecutionID})"
                        : $"Inserted: actor {fact.SourceRuntimeID} - {fact.ActionType} (#{fact.QueueActionID})";
                case SchedulerFactKind.Interrupted:
                    return $"INTERRUPTED: actor {fact.SourceRuntimeID} - {fact.ActionType} (execution {fact.ExecutionID})";
                case SchedulerFactKind.Cancelled:
                    return $"CANCELLED: actor {fact.SourceRuntimeID} (action #{fact.QueueActionID})";
                case SchedulerFactKind.Completed:
                    return $"Completed: actor {fact.SourceRuntimeID} - {fact.ActionType} (execution {fact.ExecutionID})";
                case SchedulerFactKind.Failed:
                    return $"FAILED: actor {fact.SourceRuntimeID} (execution {fact.ExecutionID})";
                case SchedulerFactKind.ContextSuspended:
                    return $"  Paused context {fact.ContextID}";
                case SchedulerFactKind.ContextActivated:
                    return fact.ContextType == AttackingStageExecutionType.NormalTurn
                        ? $"Normal turn started (context {fact.ContextID})"
                        : $"{fact.ContextType} started (context {fact.ContextID}, parent {fact.ParentContextID})";
                case SchedulerFactKind.ContextResumed:
                    return $"  Resumed context {fact.ContextID}";
                case SchedulerFactKind.ContextCompleted:
                    return $"Context {fact.ContextID} finished ({fact.ContextType})";
                case SchedulerFactKind.Removed:
                    return $"Removed pending action #{fact.QueueActionID}";
                case SchedulerFactKind.Moved:
                    return $"Reordered action #{fact.QueueActionID}";
                case SchedulerFactKind.StageSkipped:
                    return $"Stage skipped in context {fact.ContextID}";
                default:
                    // Hide Dequeued/AttemptStarted/Authorized/AttemptSettled in simple mode.
                    return null;
            }
        }

        private static string DescribeTransition(SchedulerFactPayload fact) =>
            $"{fact.Kind}: context {fact.ContextID}, queue {fact.QueueActionID}, execution {fact.ExecutionID}, source {fact.SourceRuntimeID}, cause {fact.CausingExecutionID}, {fact.Outcome}";
#endif

        public static string Describe(ActionOrderItem item) =>
            $"#{item.QueueActionID} / exec {item.ExecutionID}: actor {item.SourceRuntimeID}, {item.ActionType}, target {item.TargetMode}:{item.TargetRuntimeID}, {item.Origin}, {item.Outcome}, cause {item.CausingExecutionID}";
    }
}
