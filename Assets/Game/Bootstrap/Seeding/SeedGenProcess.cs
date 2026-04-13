using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public class SeedGenProcess : IBootstrapProcess
    {
        public int Order => SharedBootstrapOrder.Seeding;

        public void Initialise(WorldContext context)
        {
            ISeedService seedService = new SeedService();
            context.Register<ISeedService>(seedService);
            Logging.Info(LogCategory.Setup, $"World seed generated: {seedService.WorldSeed}");
        }
    }
}
