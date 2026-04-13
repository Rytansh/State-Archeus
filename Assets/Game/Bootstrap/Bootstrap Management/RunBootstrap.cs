using UnityEngine;
using Unity.Entities;
using Archeus.Core.Debugging;
using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public class RunBootstrap : MonoBehaviour
    {
        public static WorldContext RootContext { get; private set; }
        private BattleBootstrapEntry bootstrapEntry;

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
            bootstrapEntry = new BattleBootstrapEntry();

            try
            {
                Logging.DisableCategory(LogCategory.Testing);
                bootstrapEntry.Initialise();
                RootContext = bootstrapEntry.getRootContext();

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
