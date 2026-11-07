using Archeus.Battle.Components.Combat;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Turns;

namespace Archeus.Battle.Presentation.State
{
    public static class PresentationStateBuilder
    {
        public static BattlePresentationState BuildBattle(
            ulong battleRuntimeID,
            in BattleState battleState,
            in TurnCounter turnCounter,
            BattleSide activeSide
        )
        {
            return new BattlePresentationState
            {
                BattleRuntimeID = battleRuntimeID,
                Phase = battleState.Phase,
                TurnNumber = turnCounter.CurrentTurn,
                ActiveSide = activeSide,
            };
        }

        public static PlayerPresentationState BuildPlayer(
            in BattleSideAssignment team,
            in RemainingActionPoints remainingActionPoints
        )
        {
            return new PlayerPresentationState
            {
                Side = team.Side,
                ActionPoints = remainingActionPoints.Value,
            };
        }

        public static CharacterPresentationState BuildCharacter(
            in CardRuntimeID runtimeID,
            in CharacterRuntimeStatBaseline stats,
            in CurrentHealth currentHealth
        )
        {
            return new CharacterPresentationState
            {
                RuntimeID = runtimeID.Value,

                CurrentHealth = currentHealth.Value,
                MaxHealth = stats.MaxHealth,

                // Temporary until explicit CharacterLife exists.
                IsAlive = currentHealth.Value > 0,
            };
        }

        public static HandPresentationState BuildHand(
            in BattleSideAssignment team,
            in MaxHandSize maxHandSize
        )
        {
            return new HandPresentationState { Side = team.Side, MaxSize = maxHandSize.Value };
        }

        public static CardPresentationState BuildCard(
            in CardRuntimeID runtimeID,
            in CardDefinitionID definitionID,
            RuntimeCardType cardType,
            BattleSide side,
            in CardPresentationLocation location
        )
        {
            return new CardPresentationState
            {
                RuntimeID = runtimeID.Value,
                DefinitionID = definitionID.Value,
                CardType = cardType,
                Side = side,
                Location = location,
            };
        }
    }
}
