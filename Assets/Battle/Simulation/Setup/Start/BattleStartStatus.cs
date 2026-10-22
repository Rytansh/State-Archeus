using Unity.Entities;

namespace Archeus.Battle.Components.Turns { }

namespace Archeus.Battle.Components.Requests
{
    public enum BattleStartFailureReason : byte
    {
        None,
        DuplicateStartRequest,
        InvalidSetupRequest,
        InvalidBattle,
        InvalidPlayers,
        InvalidInitiative,
    }

    public struct BattleStartFailed : IComponentData
    {
        public Entity Battle;
        public BattleStartFailureReason Reason;
    }
}
