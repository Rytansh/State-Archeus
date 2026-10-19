using Unity.Entities;

namespace Archeus.Battle.Input
{
    public readonly struct BattleInputContext
    {
        public readonly Entity Battle;
        public readonly Entity Player;

        public BattleInputContext(Entity battle, Entity player)
        {
            Battle = battle;
            Player = player;
        }
    }
}
