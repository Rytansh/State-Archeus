namespace Archeus.Battle.Presentation.State
{
    public sealed class CardPresentationStateUpdate : PresentationStateUpdate
    {
        public CardPresentationState State { get; }

        public CardPresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in CardPresentationState state
        )
            : base(battleRuntimeID, revision)
        {
            State = state;
        }
    }
}
