using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public class DefaultConfigProvider : IConfigProvider
    {
        public void LoadConfigurations(WorldContext context)
        {
            //load assets/blobs later
            Logging.Info(LogCategory.Setup, "Configurations loaded successfully.");
        }
    }
}
