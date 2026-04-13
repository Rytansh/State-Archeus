namespace Archeus.Game.Bootstrap
{
    public class RNGDeterminationProcess : IBootstrapProcess
    {
        public int Order => SharedBootstrapOrder.RNG;

        public void Initialise(WorldContext context)
        {
            ISeedService seedService = context.Resolve<ISeedService>();

            IRNGProvider rngProvider = new DefaultRNGProvider(seedService);
            context.Register(rngProvider);
        }
    }
}
