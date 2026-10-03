using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattleSimulationWorld
    {
        public World EcsWorld { get; }

        public BattleSimulationWorld(World ecsWorld)
        {
            EcsWorld = ecsWorld;
        }
    }
}
