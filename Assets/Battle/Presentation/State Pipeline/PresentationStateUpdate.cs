namespace Archeus.Battle.Presentation.State
{
    public abstract class PresentationStateUpdate
    {
        public ulong BattleRuntimeID { get; }

        public ulong Revision { get; }

        protected PresentationStateUpdate(
            ulong battleRuntimeID,
            ulong revision
        )
        {
            BattleRuntimeID = battleRuntimeID;
            Revision = revision;
        }
    }
}