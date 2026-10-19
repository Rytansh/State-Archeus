using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Requests;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Battle.Input
{
    public static class DrawingInputHandler
    {
        public static void Handle(
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            in BattleInputCommand command,
            in BattleInputContext context
        )
        {
            switch (command.Type)
            {
                case BattleInputCommandType.DrawCards:
                    CreateDrawCardsRequest(ecb, context);
                    break;

                case BattleInputCommandType.PlaceCard:
                    CreatePlaceCardRequest(entityManager, ecb, command, context);
                    break;

                case BattleInputCommandType.RetreatCard:
                    CreateRetreatCardRequest(entityManager, ecb, command, context);
                    break;

                case BattleInputCommandType.EndDrawing:
                    CreateEndDrawingRequest(ecb, context);
                    break;

                default:
                    Logging.Warn(
                        LogCategory.Combat,
                        $"DrawingInputHandler received invalid command: " + $"{command.Type}."
                    );
                    break;
            }
        }

        private static void CreateDrawCardsRequest(
            EntityCommandBuffer ecb,
            in BattleInputContext context
        )
        {
            Entity request = ecb.CreateEntity();

            ecb.AddComponent(request, new DrawCardsRequest { Player = context.Player });
        }

        private static void CreatePlaceCardRequest(
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            in BattleInputCommand command,
            in BattleInputContext context
        )
        {
            if (
                !BattleInputEntityResolver.TryResolveCard(
                    entityManager,
                    context.Player,
                    command.CardRuntimeID,
                    out Entity card
                )
            )
            {
                Logging.Warn(
                    LogCategory.Combat,
                    $"Could not resolve card runtime ID " + $"{command.CardRuntimeID}."
                );

                return;
            }

            Entity request = ecb.CreateEntity();

            ecb.AddComponent(
                request,
                new PlaceCardRequest
                {
                    Player = context.Player,
                    CardToPlace = card,
                    Position = command.FieldPosition,
                }
            );
        }

        private static void CreateRetreatCardRequest(
            EntityManager entityManager,
            EntityCommandBuffer ecb,
            in BattleInputCommand command,
            in BattleInputContext context
        )
        {
            if (
                !BattleInputEntityResolver.TryResolveCard(
                    entityManager,
                    context.Player,
                    command.CardRuntimeID,
                    out Entity card
                )
            )
            {
                Logging.Warn(
                    LogCategory.Combat,
                    $"Could not resolve card runtime ID " + $"{command.CardRuntimeID}."
                );

                return;
            }

            Entity request = ecb.CreateEntity();

            ecb.AddComponent(
                request,
                new RetreatCardRequest { Player = context.Player, CardToRetreat = card }
            );
        }

        private static void CreateEndDrawingRequest(
            EntityCommandBuffer ecb,
            in BattleInputContext context
        )
        {
            Entity request = ecb.CreateEntity();

            ecb.AddComponent(request, new EndDrawingRequest { Player = context.Player });
        }
    }
}
