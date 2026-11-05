using System;
using Archeus.Battle.Snapshots;
using Archeus.Battle.Systems.Actions;
using Archeus.Battle.Systems.Effects;
using Archeus.Battle.Systems.Events;
using Archeus.Battle.Systems.Input;
using Archeus.Battle.Systems.Presentation;
using Archeus.Battle.Systems.Setup;
using Archeus.Battle.Systems.Turnflow;
using Archeus.Content.Lookup;
using Unity.Entities;

namespace Archeus.Game.Bootstrap
{
    public sealed class BattleSimulationBootstrap : ICustomBootstrap
    {
        public static World SimulationEcsWorld { get; private set; }

        public bool Initialize(string defaultWorldName)
        {
            SimulationEcsWorld = new World("Battle Simulation", WorldFlags.Simulation);

            // Unity ECS scene-streaming infrastructure
            var streamingSystems = DefaultWorldInitialization.GetAllSystems(
                WorldSystemFilterFlags.Streaming
            );

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                SimulationEcsWorld,
                streamingSystems
            );

            // Our manually-owned systems
            Type[] archeusSimulationSystems =
            {
                // Bootstrap
                typeof(BattleSimulationProbeSystem),
                typeof(BattleContentLoadSystem),
                // Content and assets
                typeof(BattleContentHandlingSystem),
                typeof(ContentLookupSystem),
                // System group initialisation
                typeof(BattleRootGroup),
                typeof(BattleSetupGroup),
                typeof(BattleCreationGroup),
                typeof(BattleInitialisationGroup),
                typeof(BattleSpawningGroup),
                typeof(BattleSimulationGroup),
                typeof(BattleInputGroup),
                typeof(TurnFlowGroup),
                typeof(TurnStartGroup),
                typeof(DrawingStageGroup),
                typeof(PlanningStageGroup),
                typeof(AttackingStageGroup),
                typeof(TurnEndGroup),
                typeof(VMSystemGroup),
                // Battle setup
                typeof(BattlePhaseTransitionSystem),
                typeof(BattleStageSnapshotSystem),
                typeof(BattleStageRevertSystem),
                typeof(BattleCreationSystem),
                typeof(RosterInitialisationSystem),
                typeof(BattleReadinessSystem),
                typeof(BattleStartSystem),
                // Turn flow
                typeof(TurnStartSystem),
                typeof(DrawingStageSystem),
                typeof(PlanningStageSystem),
                typeof(AttackingStageSystem),
                typeof(TurnEndSystem),
                // Combat
                typeof(BattleEventProcessingSystem),
                typeof(EffectDurationProcessingSystem),
                typeof(ActionExecutionSystem),
                // Presentation
                typeof(PresentationFactExportSystem),
                typeof(PresentationSnapshotExportSystem),
                typeof(PresentationUpdateExportSystem),
                typeof(PresentationStateRecoverySystem),
                // Input
                typeof(BattleInputRoutingSystem),
            };

            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(
                SimulationEcsWorld,
                archeusSimulationSystems
            );

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(SimulationEcsWorld);

            return false;
        }
    }
}
