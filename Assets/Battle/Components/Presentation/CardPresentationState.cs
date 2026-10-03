using Archeus.Battle.Components.Ownership;
using Unity.Entities;

namespace Archeus.Battle.Presentation.State
{
    public struct CardPresentationState : IComponentData
    {
        public uint RuntimeID;
        public uint DefinitionID;

        public RuntimeCardType CardType;
        public BattleSide Side;

        public CardPresentationLocation Location;
    }

    public enum CardPresentationZone : byte
    {
        None = 0,
        Deck = 1,
        Hand = 2,
        Field = 3,
    }

    public struct CardPresentationLocation
    {
        public CardPresentationZone Zone;

        private byte position;

        public static CardPresentationLocation Deck()
        {
            return new CardPresentationLocation { Zone = CardPresentationZone.Deck, position = 0 };
        }

        public static CardPresentationLocation Hand(HandPosition position)
        {
            return new CardPresentationLocation
            {
                Zone = CardPresentationZone.Hand,
                position = (byte)position,
            };
        }

        public static CardPresentationLocation Field(FieldPosition position)
        {
            return new CardPresentationLocation
            {
                Zone = CardPresentationZone.Field,
                position = (byte)position,
            };
        }

        public bool TryGetHandPosition(out HandPosition handPosition)
        {
            if (Zone != CardPresentationZone.Hand)
            {
                handPosition = default;
                return false;
            }

            handPosition = (HandPosition)position;
            return true;
        }

        public bool TryGetFieldPosition(out FieldPosition fieldPosition)
        {
            if (Zone != CardPresentationZone.Field)
            {
                fieldPosition = default;
                return false;
            }

            fieldPosition = (FieldPosition)position;
            return true;
        }
    }
}
