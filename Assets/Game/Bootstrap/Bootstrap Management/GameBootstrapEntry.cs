using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public sealed class GameBootstrapEntry
    {
        private readonly GameBootstrapOrchestrator sharedOrchestrator;
        private readonly GameBootstrapOrchestrator simulationOrchestrator;
        private readonly GameBootstrapOrchestrator presentationOrchestrator;

        private readonly WorldContext rootContext;

        public GameBootstrapEntry()
        {
            sharedOrchestrator = new GameBootstrapOrchestrator();
            simulationOrchestrator = new GameBootstrapOrchestrator();
            presentationOrchestrator = new GameBootstrapOrchestrator();

            rootContext = new WorldContext();
        }

        public void Initialise()
        {
            Logging.Info(LogCategory.Setup, "=== Game Bootstrap Started ===");

            // Shared/root services
            sharedOrchestrator.Register(new LoggingProcess());
            sharedOrchestrator.Register(new ConfigProcess());
            sharedOrchestrator.Register(new SeedGenProcess());
            sharedOrchestrator.Register(new RNGTestProcess());
            sharedOrchestrator.Register(new EventBusProcess());
            sharedOrchestrator.Register(new BattlePresentationBridgeProcess());

            // Simulation services
            simulationOrchestrator.Register(new BattleSimulationWorldProcess());

            // Presentation services
            presentationOrchestrator.Register(new BattlePresentationWorldProcess());

            sharedOrchestrator.InitialiseAll(rootContext);
            simulationOrchestrator.InitialiseAll(rootContext);
            presentationOrchestrator.InitialiseAll(rootContext);
        }

        public WorldContext GetRootContext()
        {
            return rootContext;
        }
    }
}
