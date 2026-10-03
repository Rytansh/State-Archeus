using System;

namespace Archeus.Battle.Presentation.State
{
    public sealed class PresentationStateSnapshot
    {
        public BattlePresentationState Battle;
        public ulong Revision;

        public CharacterPresentationState[] Characters = Array.Empty<CharacterPresentationState>();
        public HandPresentationState[] Hands = Array.Empty<HandPresentationState>();
        public CardPresentationState[] Cards = Array.Empty<CardPresentationState>();
        public PlayerPresentationState[] Players = Array.Empty<PlayerPresentationState>();
    }
}
