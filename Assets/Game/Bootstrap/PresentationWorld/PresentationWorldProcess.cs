using Archeus.Core.Debugging;

namespace Archeus.Game.Bootstrap
{
    public class PresentationWorldProcess : IBootstrapProcess
    {
        public int Order => PresentationBootstrapOrder.PresentationWorld;

        public void Initialise(WorldContext rootContext)
        {
            PresentationWorld presentationWorld = new PresentationWorld(rootContext);
            presentationWorld.Initialise();

            rootContext.Register<PresentationWorld>(presentationWorld);

            Logging.Info(LogCategory.Setup,"Presentation world initialised.");
        }
    }
}

