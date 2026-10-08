using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Input
{
    public static class BattleInputEntityResolver
    {
        public static bool TryResolveCard(
            EntityManager entityManager,
            Entity player,
            uint runtimeID,
            out Entity card
        )
        {
            card = Entity.Null;

            if (player == Entity.Null || !entityManager.Exists(player))
            {
                return false;
            }

            if (TryResolveCardInHand(entityManager, player, runtimeID, out card))
            {
                return true;
            }

            if (TryResolveCardOnField(entityManager, player, runtimeID, out card))
            {
                return true;
            }

            if (TryResolveCardInDeck(entityManager, player, runtimeID, out card))
            {
                return true;
            }

            return false;
        }

        private static bool TryResolveCardInHand(
            EntityManager entityManager,
            Entity player,
            uint runtimeID,
            out Entity card
        )
        {
            card = Entity.Null;

            if (!entityManager.HasBuffer<HandCard>(player))
                return false;

            DynamicBuffer<HandCard> hand = entityManager.GetBuffer<HandCard>(player);

            for (int i = 0; i < hand.Length; i++)
            {
                Entity candidate = hand[i].Card;

                if (MatchesRuntimeID(entityManager, candidate, runtimeID))
                {
                    card = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveCardOnField(
            EntityManager entityManager,
            Entity player,
            uint runtimeID,
            out Entity card
        )
        {
            card = Entity.Null;

            if (!entityManager.HasBuffer<FieldCard>(player))
                return false;

            DynamicBuffer<FieldCard> field = entityManager.GetBuffer<FieldCard>(player);

            for (int i = 0; i < field.Length; i++)
            {
                Entity candidate = field[i].Card;

                if (MatchesRuntimeID(entityManager, candidate, runtimeID))
                {
                    card = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveCardInDeck(
            EntityManager entityManager,
            Entity player,
            uint runtimeID,
            out Entity card
        )
        {
            card = Entity.Null;

            if (!entityManager.HasBuffer<DeckCard>(player))
                return false;

            DynamicBuffer<DeckCard> deck = entityManager.GetBuffer<DeckCard>(player);

            for (int i = 0; i < deck.Length; i++)
            {
                Entity candidate = deck[i].Card;

                if (MatchesRuntimeID(entityManager, candidate, runtimeID))
                {
                    card = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesRuntimeID(
            EntityManager entityManager,
            Entity card,
            uint runtimeID
        )
        {
            return card != Entity.Null
                && entityManager.Exists(card)
                && entityManager.HasComponent<CardRuntimeID>(card)
                && entityManager.GetComponentData<CardRuntimeID>(card).Value == runtimeID;
        }
    }
}
