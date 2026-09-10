using Archeus.Core.Debugging;
using Unity.Entities;
using UnityEngine;

namespace Archeus.Game.Bootstrap
{
    public class RunBootstrap : MonoBehaviour
    {
        public static WorldContext RootContext { get; private set; }
        private GameBootstrapEntry bootstrapEntry;

        [Header("Bootstrap Settings")]
        [Tooltip("Run bootstrap on Awake automatically.")]
        public bool autoRun = true;

        private void Awake()
        {
            if (autoRun)
                Run();

            DontDestroyOnLoad(gameObject);
        }

        public void Run()
        {
            bootstrapEntry = new GameBootstrapEntry();
            Logging.DisableCategory(LogCategory.VM);
            Logging.DisableCategory(LogCategory.RNG);
            Logging.DisableCategory(LogCategory.Event);
            Logging.DisableCategory(LogCategory.Setup);
            //Logging.DisableCategory(LogCategory.Testing);
            //Logging.DisableCategory(LogCategory.Simulation);
            //Logging.DisableCategory(LogCategory.Presentation);
            try
            {
                bootstrapEntry.Initialise();
                RootContext = bootstrapEntry.GetRootContext();

                World ecsWorld = World.DefaultGameObjectInjectionWorld;

                EntityManager em = ecsWorld.EntityManager;

                em.CreateEntity(typeof(GameBootstrapCompleteTag));
            }
            catch (System.Exception)
            {
                Logging.Error(LogCategory.Setup, "FATAL ERROR - Game Bootstrap failed.");
            }
        }
    }
}
