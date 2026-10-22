using Unity.Entities;

namespace Archeus.Battle.Components.Core
{
    public struct BattleState : IComponentData
    {
        public BattlePhase Phase;
    }

    public enum BattlePhase : byte
    {
        Setup = 0,

        TurnStart = 50,
        Drawing = 60,
        Planning = 70,
        Attacking = 80,
        TurnEnd = 90,

        Victory = 100,
        Defeat = 101,
    }
}
