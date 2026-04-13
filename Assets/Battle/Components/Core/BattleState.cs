using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleState : IComponentData
    {
        public BattlePhase Phase;
    }

    public enum BattlePhase : byte
    {
        Creating = 0,
        Initialising = 1,
        Spawning = 2,

        WaitingForBattleReadySignal = 9,
        BattleReady = 10,

        TurnStart = 20,
        Drawing = 30,
        Planning = 40,
        Attacking = 50,
        TurnEnd = 60,

        Victory = 100,
        Defeat = 101
    }

}
