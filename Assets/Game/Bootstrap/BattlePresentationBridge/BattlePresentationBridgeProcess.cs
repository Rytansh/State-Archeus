using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattlePresentationBridgeProcess : IBootstrapProcess
    {
        public int Order => SharedBootstrapOrder.BattlePresentationBridge;

        public void Initialise(WorldContext rootContext)
        {
            BattlePresentationBridge bridge = new BattlePresentationBridge();

            rootContext.Register(bridge);

            Logging.Info(LogCategory.Setup, "Battle presentation bridge initialised.");
        }
    }
}
