using Unity.Entities;

namespace Archeus.Battle.Components.Requests
{
    public struct BattleStartRequest : IComponentData
    {
        public Entity Battle;
    }

    public enum BattleReadinessFailureReason : byte
    {
        None,
        DuplicateReadinessRequest,
        InvalidSetupRequest,
        InvalidBattle,
        PlayerMismatch,
        LeadMismatch,
        DeckMismatch,
        ParticipantMismatch,
        UnregisteredRuntimeEntity,
    }

    public struct BattleReadinessFailed : IComponentData
    {
        public Entity Battle;
        public BattleReadinessFailureReason Reason;
    }
}
