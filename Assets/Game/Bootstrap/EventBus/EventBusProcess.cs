using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public class EventBusProcess : IBootstrapProcess
    {
        public int Order => SimulationBootstrapOrder.EventBus;

        public void Initialise(WorldContext rootContext)
        {
            IEventBus eventBus = new DefaultEventBus();
            rootContext.Register<IEventBus>(eventBus);

            Logging.Info(LogCategory.Setup,"Event Bus initialised.");
        }
    }
}
