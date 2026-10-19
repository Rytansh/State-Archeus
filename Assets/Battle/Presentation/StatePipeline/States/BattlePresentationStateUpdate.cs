namespace Archeus.Battle.Presentation.State
{
    public sealed class BattlePresentationStateUpdate : PresentationStateUpdate
    {
        public BattlePresentationState State { get; }

        public BattlePresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision,
            in BattlePresentationState state
        )
            : base(battleRuntimeID, revision)
        {
            State = state;
        }
    }
}
