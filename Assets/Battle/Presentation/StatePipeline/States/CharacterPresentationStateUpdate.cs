namespace Archeus.Battle.Presentation.State
{
    public sealed class CharacterPresentationStateUpdate : PresentationStateUpdate
    {
        public CharacterPresentationState State { get; }

        public CharacterPresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in CharacterPresentationState state
        )
            : base(battleRuntimeID, revision)
        {
            State = state;
        }
    }
}
