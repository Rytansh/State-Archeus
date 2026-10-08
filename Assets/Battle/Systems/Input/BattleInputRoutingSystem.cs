using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Input;
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
                    .Query<RefRO<OwnedBattle>, RefRO<Team>>()
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
