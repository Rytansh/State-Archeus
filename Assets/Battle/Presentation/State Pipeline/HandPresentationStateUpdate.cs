namespace Archeus.Battle.Presentation.State
{
    public sealed class HandPresentationStateUpdate : PresentationStateUpdate
    {
        public HandPresentationState State { get; }

        public HandPresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in HandPresentationState state
        )
            : base(battleRuntimeID, revision)
        {
            State = state;
        }
    }
}
