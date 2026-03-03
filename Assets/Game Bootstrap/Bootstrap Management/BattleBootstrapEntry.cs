using UnityEngine;

public sealed class BattleBootstrapEntry
{
    private readonly BattleBootstrapOrchestrator sharedOrchestrator;
    private readonly BattleBootstrapOrchestrator simulationOrchestrator;
    private readonly BattleBootstrapOrchestrator presentationOrchestrator;
    private readonly WorldContext rootContext;

    public BattleBootstrapEntry()
    {
        sharedOrchestrator = new BattleBootstrapOrchestrator();
        simulationOrchestrator = new BattleBootstrapOrchestrator();
        presentationOrchestrator = new BattleBootstrapOrchestrator();
        rootContext = new WorldContext();
    }

    public void Initialise()
    {
        Logging.System("=== Game Bootstrap Started ===");

        // Register all processes (in any order)
        sharedOrchestrator.Register(new LoggingProcess());
        sharedOrchestrator.Register(new ConfigProcess());
        sharedOrchestrator.Register(new SeedGenProcess());
        sharedOrchestrator.Register(new RNGDeterminationProcess());
        sharedOrchestrator.Register(new RNGTestProcess());
        simulationOrchestrator.Register(new SimulationWorldProcess());
        simulationOrchestrator.Register(new EventBusProcess());
        presentationOrchestrator.Register(new PresentationWorldProcess());

        sharedOrchestrator.InitialiseAll(rootContext);
        simulationOrchestrator.InitialiseAll(rootContext);
        presentationOrchestrator.InitialiseAll(rootContext);

        Logging.System("=== Battle Bootstrap Completed ===");
    }

    public WorldContext getRootContext()
    {
        return rootContext;
    }
}

