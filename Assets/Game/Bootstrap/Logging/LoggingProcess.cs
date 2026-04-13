using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public class LoggingProcess : IBootstrapProcess
    {
        public int Order => SharedBootstrapOrder.Logging;

        public void Initialise(WorldContext context)
        {
            // Register a logger service (in HSR, this later forwards to internal tools)
            ILoggingService logger = new DefaultLoggingService();
            context.Register<ILoggingService>(logger);

            Logging.Info(LogCategory.Setup,"Logging service registered to root context.");
        }
    }
}

