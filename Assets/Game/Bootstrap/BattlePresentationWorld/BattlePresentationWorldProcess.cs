using System;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation;
using Archeus.Battle.Presentation.Presenters;
using Archeus.Battle.Systems.Presentation;
using Archeus.Core.Debugging;
using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattlePresentationWorldProcess : IBootstrapProcess
    {
        public int Order => PresentationBootstrapOrder.BattlePresentationWorld;

        public void Initialise(WorldContext rootContext)
        {
            World ecsWorld = new World("Battle Presentation", WorldFlags.Simulation);

            Type[] presentationSystems =
            {
                typeof(BattlePresentationProbeSystem),
                typeof(PresentationFactImportSystem),
                typeof(BattlePresenterRegistrationSystem),
                typeof(BattlePresentationGroup),
                typeof(BattlePresentationAssemblySystem),
                typeof(PresentationPacketProbeSystem),
                typeof(PresentationPlanCompilerSystem),
                typeof(PresentationSchedulingSystem),
            };

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                ecsWorld,
                presentationSystems
            );

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(ecsWorld);

            BattlePresentationWorld presentationWorld = new BattlePresentationWorld(ecsWorld);

            rootContext.Register(presentationWorld);

            // Create reference to the presentation bridge
            BattlePresentationBridge bridge = rootContext.Resolve<BattlePresentationBridge>();
            EntityManager entityManager = presentationWorld.EcsWorld.EntityManager;
            Entity bridgeEntity = entityManager.CreateEntity();
            entityManager.SetName(bridgeEntity, "Battle Presentation Bridge Reference");
            entityManager.AddComponentObject(
                bridgeEntity,
                new BattlePresentationBridgeReference { Bridge = bridge }
            );

            // Create reference to the presentation registry
            PresentationRegistry registry = new PresentationRegistry();
            Entity registryEntity = entityManager.CreateEntity();
            entityManager.SetName(registryEntity, "Presentation Registry Reference");
            entityManager.AddComponentObject(
                registryEntity,
                new PresentationRegistryReference { Registry = registry }
            );

            // Create reference to the presentation RECIPE registry
            PresentationRecipeRegistry recipeRegistry = new PresentationRecipeRegistry();
            Entity recipeRegistryEntity = entityManager.CreateEntity();
            entityManager.SetName(recipeRegistryEntity, "Presentation Recipe Registry Reference");
            entityManager.AddComponentObject(
                recipeRegistryEntity,
                new PresentationRecipeRegistryReference { Registry = recipeRegistry }
            );

            Logging.Info(
                LogCategory.Setup,
                $"Battle Presentation created using ECS World: {ecsWorld.Name}"
            );
        }
    }
}
