using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Requests;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Components.Turns;
using Archeus.Battle.Input;
using Archeus.Battle.Snapshots;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;
using Unity.VisualScripting;

namespace Archeus.Battle.Systems.Input
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleInputGroup))]
    public partial struct BattleInputRoutingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BattleInputInboxTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var commands in SystemAPI
                    .Query<DynamicBuffer<BattleInputCommand>>()
                    .WithAll<BattleInputInboxTag>()
            )
            {
                for (int i = 0; i < commands.Length; i++)
                {
                    // Reversion wins over every other command for this battle in this batch.
                    // This prevents a queued placement/commit from executing after its revert.
                    bool abandoned = false;
                    for (int j = 0; j < commands.Length; j++)
                        if (
                            commands[j].BattleID == commands[i].BattleID
                            && commands[j].Type == BattleInputCommandType.RevertStage
                            && commands[j].Type != commands[i].Type
                            && CanRevertCommand(ref state, commands[j])
                        )
                            abandoned = true;
                    if (abandoned)
                        continue;
                    ProcessCommand(ref state, ecb, commands[i]);
                }

                commands.Clear();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessCommand(
            ref SystemState state,
            EntityCommandBuffer ecb,
            BattleInputCommand command
        )
        {
            if (!TryResolveContext(ref state, command, out BattleInputContext context))
            {
                return;
            }

            if (!state.EntityManager.HasComponent<StageSnapshotStatus>(context.Battle))
                return;
            StageSnapshotStatus status = state.EntityManager.GetComponentData<StageSnapshotStatus>(
                context.Battle
            );
            if (!status.IsValid || status.Revision != command.StageRevision)
                return;

            switch (command.Type)
            {
                // Drawing
                case BattleInputCommandType.DrawCards:
                case BattleInputCommandType.PlaceCard:
                case BattleInputCommandType.RetreatCard:
                case BattleInputCommandType.EndDrawing:
                {
                    DrawingInputHandler.Handle(state.EntityManager, ecb, command, context);

                    break;
                }

                case BattleInputCommandType.EndPlanning:
                {
                    Entity request = ecb.CreateEntity();
                    ecb.AddComponent(request, new EndPlanningRequest { Player = context.Player });
                    ecb.AddComponent(
                        request,
                        new StageCommandOwner
                        {
                            Battle = context.Battle,
                            Revision = command.StageRevision,
                        }
                    );
                    break;
                }
                case BattleInputCommandType.RevertStage:
                {
                    Entity request = ecb.CreateEntity();
                    ecb.AddComponent(
                        request,
                        new RevertStageRequest
                        {
                            Player = context.Player,
                            ExpectedRevision = command.StageRevision,
                        }
                    );
                    ecb.AddComponent(
                        request,
                        new StageCommandOwner
                        {
                            Battle = context.Battle,
                            Revision = command.StageRevision,
                        }
                    );
                    break;
                }

                // Future Planning
                /*
                case BattleInputCommandType.QueueAction:
                case BattleInputCommandType.CancelAction:
                case BattleInputCommandType.EndPlanning:
                {
                    PlanningInputHandler.Handle(
                        state.EntityManager,
                        ecb,
                        command,
                        context
                    );

                    break;
                }
                */

                default:
                {
                    Logging.Warn(
                        LogCategory.Combat,
                        $"Unhandled battle input command: {command.Type}."
                    );

                    break;
                }
            }
        }

        private bool CanRevertCommand(ref SystemState state, BattleInputCommand command)
        {
            if (!TryResolveContext(ref state, command, out BattleInputContext context))
                return false;
            var entities = state.EntityManager;
            if (
                !entities.HasComponent<StageSnapshotStatus>(context.Battle)
                || !entities.HasComponent<ActiveTurnPlayer>(context.Battle)
                || !entities.HasComponent<BattleState>(context.Battle)
            )
                return false;
            var status = entities.GetComponentData<StageSnapshotStatus>(context.Battle);
            var phase = entities.GetComponentData<BattleState>(context.Battle).Phase;
            return status.IsValid
                && status.Revision == command.StageRevision
                && entities.GetComponentData<ActiveTurnPlayer>(context.Battle).Player
                    == context.Player
                && (phase == BattlePhase.Drawing || phase == BattlePhase.Planning)
                && !entities.HasComponent<BattleDrawingCompleteTag>(context.Battle)
                && !entities.HasComponent<BattlePlanningCompleteTag>(context.Battle);
        }

        private bool TryResolveContext(
            ref SystemState state,
            BattleInputCommand command,
            out BattleInputContext context
        )
        {
            context = default;

            if (!TryResolveBattle(ref state, command.BattleID, out Entity battle))
            {
                Logging.Warn(LogCategory.Combat, $"Could not resolve battle {command.BattleID}.");

                return false;
            }

            if (!TryResolvePlayer(ref state, battle, command.Side, out Entity player))
            {
                Logging.Warn(
                    LogCategory.Combat,
                    $"Could not resolve player {command.Side} " + $"for battle {command.BattleID}."
                );

                return false;
            }

            context = new BattleInputContext(battle, player);

            return true;
        }

        private bool TryResolveBattle(ref SystemState state, ulong battleID, out Entity battle)
        {
            battle = Entity.Null;

            foreach (
                var (id, entity) in SystemAPI
                    .Query<RefRO<BattleID>>()
                    .WithAll<BattleTag>()
                    .WithEntityAccess()
            )
            {
                if (id.ValueRO.Value != battleID)
                    continue;

                battle = entity;
                return true;
            }

            return false;
        }

        private bool TryResolvePlayer(
            ref SystemState state,
            Entity battle,
            BattleSide side,
            out Entity player
        )
        {
            player = Entity.Null;

            foreach (
                var (ownedBattle, team, entity) in SystemAPI
                    .Query<RefRO<OwnedBattle>, RefRO<BattleSideAssignment>>()
                    .WithAll<PlayerTag>()
                    .WithEntityAccess()
            )
            {
                if (ownedBattle.ValueRO.Battle != battle)
                    continue;

                if (team.ValueRO.Side != side)
                    continue;

                player = entity;
                return true;
            }

            return false;
        }
    }
}
