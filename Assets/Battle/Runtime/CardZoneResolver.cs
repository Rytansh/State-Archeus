using Archeus.Battle.Components.Core;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    public static class CardZoneResolver
    {
        public static bool TryDrawRandomCard(
            ref BattleRNG battleRNG,
            ref DynamicBuffer<DeckCard> deck,
            ref DynamicBuffer<HandCard> hand,
            int maxHandSize,
            out Entity drawnCard
        )
        {
            drawnCard = Entity.Null;

            if (hand.Length >= maxHandSize || deck.Length == 0)
                return false;

            if (!TryFindFreeHandPosition(hand, out HandPosition handPosition))
                return false;

            int cardIndex = BattleRNGService.RollInt(ref battleRNG, 0, deck.Length);

            drawnCard = deck[cardIndex].Card;
            hand.Add(new HandCard { Card = drawnCard, Position = handPosition });
            deck.RemoveAt(cardIndex);

            return true;
        }

        public static bool TryDeployCard(
            Entity card,
            FieldPosition position,
            ref DynamicBuffer<HandCard> hand,
            ref DynamicBuffer<FieldCard> field
        )
        {
            int handIndex = -1;

            for (int i = 0; i < hand.Length; i++)
            {
                if (hand[i].Card == card)
                {
                    handIndex = i;
                    break;
                }
            }

            if (handIndex < 0) // not in hand
                return false;

            if (!IsFieldPositionFree(position, field))
            {
                return false;
            }

            field.Add(new FieldCard { Card = card, Position = position });
            hand.RemoveAt(handIndex);

            return true;
        }

        public static bool TryRetreatCard(
            Entity card,
            ref DynamicBuffer<FieldCard> field,
            ref DynamicBuffer<HandCard> hand,
            int maxHandSize
        )
        {
            return false;
        }

        private static bool TryFindFreeHandPosition(
            DynamicBuffer<HandCard> hand,
            out HandPosition position
        )
        {
            for (int slot = 0; slot < 4; slot++)
            {
                HandPosition candidate = (HandPosition)slot;

                bool occupied = false;

                for (int i = 0; i < hand.Length; i++)
                {
                    if (hand[i].Position == candidate)
                    {
                        occupied = true;
                        break;
                    }
                }

                if (!occupied)
                {
                    position = candidate;
                    return true;
                }
            }

            position = default;
            return false;
        }

        private static bool IsFieldPositionFree(
            FieldPosition position,
            DynamicBuffer<FieldCard> field
        )
        {
            for (int i = 0; i < field.Length; i++)
            {
                if (field[i].Position == position)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
