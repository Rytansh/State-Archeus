namespace Archeus.Game.Bootstrap
{
    public class ConfigProcess : IBootstrapProcess
    {
        public int Order => SimulationBootstrapOrder.Config;

        public void Initialise(WorldContext context)
        {
            IConfigRegistry configRegistry = new ConfigRegistry();
            context.Register<IConfigRegistry>(configRegistry);
            
            IConfigProvider configProvider = new DefaultConfigProvider();
            configProvider.LoadConfigurations(context);
            context.Register<IConfigProvider>(configProvider);
        }
    }
}
