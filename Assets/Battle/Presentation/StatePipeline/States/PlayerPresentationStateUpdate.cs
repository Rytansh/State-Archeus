namespace Archeus.Battle.Presentation.State
{
    public sealed class PlayerPresentationStateUpdate : PresentationStateUpdate
    {
        public PlayerPresentationState State { get; }

        public PlayerPresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in PlayerPresentationState state
        )
            : base(battleRuntimeID, revision)
        {
            State = state;
        }
    }
}
