using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public sealed class SimulationWorld
    {
        public WorldContext localContext { get; private set; }
        private readonly WorldContext rootContext;

        public SimulationWorld(WorldContext rootContext)
        {
            this.rootContext = rootContext;
        }

        public void Initialise()
        {
            localContext = new WorldContext();

            ISeedService rootSeedService = rootContext.Resolve<ISeedService>();
            ulong localSeed = rootSeedService.CreateDerivedSeed("Battle");
            SimulationRNGState RNGState = new SimulationRNGState {battleRNG = new DeterministicRNG(localSeed)};

            localContext.Register<SimulationRNGState>(RNGState);

            Logging.Info(LogCategory.Setup,"SimulationWorld context created.");
        }

        public sealed class SimulationRNGState
        {
            public DeterministicRNG battleRNG;
        }

        public void Dispose()
        {
            // Clean-up logic later
            Logging.Info(LogCategory.Setup,"SimulationWorld disposed.");
        }
    }
}

