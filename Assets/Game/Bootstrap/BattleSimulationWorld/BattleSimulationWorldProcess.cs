using Archeus.Battle.Buffers.Input;
using Archeus.Battle.Components.Tags;
using Archeus.Core.Debugging;
using Archeus.Game.Input;
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

            EntityManager entityManager = simulationWorld.EcsWorld.EntityManager;

            CreatePresentationBridgeReference(rootContext, entityManager);

            CreateBattleInputInfrastructure(rootContext, entityManager);

            Logging.Info(
                LogCategory.Setup,
                $"Simulation world registered using ECS World: {ecsWorld.Name}"
            );
        }

        private static void CreatePresentationBridgeReference(
            WorldContext rootContext,
            EntityManager entityManager
        )
        {
            BattlePresentationBridge bridge = rootContext.Resolve<BattlePresentationBridge>();

            Entity bridgeEntity = entityManager.CreateEntity();

            entityManager.SetName(bridgeEntity, "Battle Presentation Bridge Reference");

            entityManager.AddComponentObject(
                bridgeEntity,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );
        }

        private static void CreateBattleInputInfrastructure(
            WorldContext rootContext,
            EntityManager entityManager
        )
        {
            Entity inputInbox = entityManager.CreateEntity();

            entityManager.SetName(inputInbox, "Battle Input Inbox");

            entityManager.AddComponent<BattleInputInboxTag>(inputInbox);

            entityManager.AddBuffer<BattleInputCommand>(inputInbox);

            BattleInputGateway inputGateway = new BattleInputGateway(entityManager, inputInbox);

            rootContext.Register(inputGateway);
        }
    }
}
