using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattleSimulationWorldProcess : IBootstrapProcess
    {
        public int Order => SimulationBootstrapOrder.BattleSimulationWorld;

        public void Initialise(WorldContext rootContext)
        {
            World ecsWorld = BattleSimulationBootstrap.SimulationEcsWorld;

            if (ecsWorld == null || !ecsWorld.IsCreated)
            {
                throw new System.InvalidOperationException(
                    "Archeus Simulation ECS World has not been created."
                );
            }

            BattleSimulationWorld simulationWorld = new BattleSimulationWorld(ecsWorld);

            rootContext.Register(simulationWorld);

            // Create reference to the presentation bridge
            BattlePresentationBridge bridge = rootContext.Resolve<BattlePresentationBridge>();
            EntityManager entityManager = simulationWorld.EcsWorld.EntityManager;
            Entity bridgeEntity = entityManager.CreateEntity();
            entityManager.SetName(bridgeEntity, "Battle Presentation Bridge Reference");
            entityManager.AddComponentObject(
                bridgeEntity,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );

            Logging.Info(
                LogCategory.Setup,
                $"Simulation world registered using ECS World: " + $"{ecsWorld.Name}"
            );
        }
    }
}
