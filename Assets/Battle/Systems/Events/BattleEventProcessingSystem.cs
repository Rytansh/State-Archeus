using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Buffers.Combat;
using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Buffers.VM;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Components.Ownership;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Components.Stats;
using Archeus.Battle.Components.Tags;
using Archeus.Battle.Data.Events;
using Archeus.Battle.Events.Context;
using Archeus.Battle.Events.Resolvers;
using Archeus.Battle.Presentation.Factory;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.VM.Execution;
using Archeus.Content.Registries;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

namespace Archeus.Battle.Systems.Events
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(BattleSimulationGroup))]
    public partial struct BattleEventProcessingSystem : ISystem
    {
        private const int MAX_EXECUTIONS = 10000;

        private ComponentLookup<CharacterStats> characterStatsLookup;
        private ComponentLookup<CurrentHealth> characterHPLookup;
        private ComponentLookup<BattleRNG> battleRNGLookup;
        private ComponentLookup<CardRuntimeID> cardRuntimeIDLookup;

        private BufferLookup<BehaviourRuntimeState> behaviourStateLookup;
        private BufferLookup<ActiveEffect> activeEffectsLookup;
        private BufferLookup<BattleParticipant> participantLookup;

        private ComponentLookup<Team> teamLookup;

        public void OnCreate(ref SystemState state)
        {
            characterStatsLookup = state.GetComponentLookup<CharacterStats>(true);
            characterHPLookup = state.GetComponentLookup<CurrentHealth>();
            battleRNGLookup = state.GetComponentLookup<BattleRNG>();
            cardRuntimeIDLookup = state.GetComponentLookup<CardRuntimeID>(true);
            behaviourStateLookup = state.GetBufferLookup<BehaviourRuntimeState>();
            activeEffectsLookup = state.GetBufferLookup<ActiveEffect>();
            participantLookup = state.GetBufferLookup<BattleParticipant>();
            teamLookup = state.GetComponentLookup<Team>();
        }

        public void OnUpdate(ref SystemState state)
        {
            characterStatsLookup.Update(ref state);
            characterHPLookup.Update(ref state);
            battleRNGLookup.Update(ref state);
            cardRuntimeIDLookup.Update(ref state);
            behaviourStateLookup.Update(ref state);
            activeEffectsLookup.Update(ref state);
            participantLookup.Update(ref state);
            teamLookup.Update(ref state);

            foreach (
                var (
                    mainEventQueue,
                    chainedEventQueue,
                    executionRequestQueue,
                    frameIDCounter,
                    battle
                ) in SystemAPI
                    .Query<
                        DynamicBuffer<BattleEvent>,
                        DynamicBuffer<ChainedBattleEvent>,
                        DynamicBuffer<BehaviourExecutionRequest>,
                        RefRW<BattleEventFrameIDCounter>
                    >()
                    .WithAll<BattleTag>()
                    .WithEntityAccess()
            )
            {
                if (
                    mainEventQueue.Length == 0
                    && chainedEventQueue.Length == 0
                    && executionRequestQueue.Length == 0
                )
                    continue;
                if (!SystemAPI.HasComponent<BattleContentRegistry>(battle))
                {
                    Logging.Warn(LogCategory.System, "Missing Battle Content registry.");
                    continue;
                }
                if (!participantLookup.HasBuffer(battle))
                {
                    Logging.Warn(LogCategory.System, "Missing Participant lookup buffer.");
                    continue;
                }
                if (!SystemAPI.HasBuffer<PresentationFact>(battle))
                {
                    Logging.Warn(LogCategory.System, "Missing Presentation Fact buffer.");
                    continue;
                }

                if (!SystemAPI.HasComponent<PresentationSequenceCounter>(battle))
                {
                    Logging.Warn(LogCategory.System, "Missing Presentation Sequence Counter.");
                    continue;
                }

                if (!SystemAPI.HasComponent<BattleID>(battle))
                {
                    Logging.Warn(LogCategory.System, "Missing Battle ID.");
                    continue;
                }

                BlobAssetReference<ContentBlobRegistry> battleRegistryReference = SystemAPI
                    .GetComponent<BattleContentRegistry>(battle)
                    .BattleRegistryReference;
                DynamicBuffer<BattleParticipant> participants = participantLookup[battle];
                DynamicBuffer<PresentationFact> presentationFactQueue =
                    SystemAPI.GetBuffer<PresentationFact>(battle);
                DynamicBuffer<ActionExecutionState> actionExecutionStates =
                    SystemAPI.GetBuffer<ActionExecutionState>(battle);
                RefRW<PresentationSequenceCounter> presentationSequenceCounter =
                    SystemAPI.GetComponentRW<PresentationSequenceCounter>(battle);
                RefRW<BattleOperationIDCounter> operationCounter =
                    SystemAPI.GetComponentRW<BattleOperationIDCounter>(battle);

                ulong battleID = SystemAPI.GetComponent<BattleID>(battle).Value;

                BattleContext ctx = new BattleContext
                {
                    Battle = battle,
                    BattleID = battleID,

                    ChainedEventQueue = chainedEventQueue,
                    ActionExecutionStates = actionExecutionStates,
                    OperationCounter = operationCounter,

                    PresentationFactQueue = presentationFactQueue,
                    PresentationSequenceCounter = presentationSequenceCounter,

                    StatsLookup = characterStatsLookup,
                    HealthLookup = characterHPLookup,
                    RNGLookup = battleRNGLookup,

                    CardRuntimeIDLookup = cardRuntimeIDLookup,
                    EffectLookup = activeEffectsLookup,

                    Participants = participants,
                    TeamLookup = teamLookup,

                    BattleRegistryReference = battleRegistryReference,
                };

                NativeList<EventFrame> eventFrames = new NativeList<EventFrame>(64, Allocator.Temp);
                NativeList<AbilityExecutionContinuation> continuations =
                    new NativeList<AbilityExecutionContinuation>(16, Allocator.Temp);

                SeedRootFrames(mainEventQueue, ref eventFrames, frameIDCounter);

                int safetyCounter = 0;
                int previousGeneration = -1;

                while (true)
                {
                    PromoteChainedEvents(chainedEventQueue, ref eventFrames, frameIDCounter);

                    if (
                        TryResumeReadyContinuation(
                            ref continuations,
                            ref eventFrames,
                            chainedEventQueue,
                            executionRequestQueue,
                            ref ctx
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        !TryGetLowestActiveGeneration(
                            in eventFrames,
                            executionRequestQueue,
                            out ushort activeGeneration
                        )
                    )
                    {
                        if (continuations.Length > 0)
                        {
                            Logging.Warn(
                                LogCategory.Event,
                                "[Scheduler] No runnable event work exists "
                                    + "but VM continuations are still waiting."
                            );
                        }

                        break;
                    }

                    if (activeGeneration != previousGeneration)
                    {
                        previousGeneration = activeGeneration;

                        Logging.Info(
                            LogCategory.Event,
                            $"Processing generation " + $"{activeGeneration}."
                        );
                    }

                    if (++safetyCounter > MAX_EXECUTIONS)
                    {
                        Logging.Warn(
                            LogCategory.Event,
                            "[Battle] Fatal error - TOO MANY EVENT EXECUTIONS - clearing."
                        );

                        mainEventQueue.Clear();
                        chainedEventQueue.Clear();
                        executionRequestQueue.Clear();

                        break;
                    }

                    int requestIndex = FindBestExecutionRequestIndex(
                        executionRequestQueue,
                        in eventFrames,
                        activeGeneration
                    );

                    if (requestIndex >= 0)
                    {
                        BehaviourExecutionRequest request = executionRequestQueue[requestIndex];
                        executionRequestQueue.RemoveAt(requestIndex);
                        ExecuteBehaviourRequest(
                            request,
                            ref eventFrames,
                            ref continuations,
                            ref ctx
                        );
                        continue;
                    }

                    int frameIndex = FindTopFrameIndexForGeneration(
                        in eventFrames,
                        activeGeneration
                    );

                    if (frameIndex < 0)
                    {
                        Logging.Warn(
                            LogCategory.Event,
                            $"[Scheduler] Generation "
                                + $"{activeGeneration} was reported active "
                                + $"but no runnable work could be found."
                        );

                        break;
                    }

                    ref EventFrame frame = ref eventFrames.ElementAt(frameIndex);

                    ProcessFrameStep(ref state, ref ctx, ref frame, executionRequestQueue);
                }

                EmitCompletedActionFacts(
                    ref ctx,
                    in eventFrames,
                    chainedEventQueue,
                    executionRequestQueue,
                    in continuations
                );

                continuations.Dispose();
                eventFrames.Dispose();
            }
        }

        private void SeedRootFrames(
            DynamicBuffer<BattleEvent> mainEventQueue,
            ref NativeList<EventFrame> eventFrames,
            RefRW<BattleEventFrameIDCounter> frameIDCounter
        )
        {
            for (int i = mainEventQueue.Length - 1; i >= 0; i--)
            {
                EventFrame rootFrame = new EventFrame
                {
                    ID = RetrieveNextFrameID(frameIDCounter),
                    Event = mainEventQueue[i],
                    Phase = BattleEventPhase.PreResolution,
                    PhaseStarted = false,
                    Completed = false,
                };

                ValidateFrame(in rootFrame);
                // LogFrameCreated("ROOT", in rootFrame);

                eventFrames.Add(rootFrame);
            }

            mainEventQueue.Clear();
        }

        private void PromoteChainedEvents(
            DynamicBuffer<ChainedBattleEvent> chainedEventQueue,
            ref NativeList<EventFrame> eventFrames,
            RefRW<BattleEventFrameIDCounter> frameIDCounter
        )
        {
            for (int i = chainedEventQueue.Length - 1; i >= 0; i--)
            {
                BattleEvent evt = chainedEventQueue[i].Event;

                EventFrame chainedFrame = new EventFrame
                {
                    ID = RetrieveNextFrameID(frameIDCounter),
                    Event = evt,
                    Phase = BattleEventPhase.PreResolution,
                    PhaseStarted = false,
                    Completed = false,
                };

                ValidateFrame(in chainedFrame);
                eventFrames.Add(chainedFrame);
            }

            chainedEventQueue.Clear();
        }

        private bool TryGetLowestActiveGeneration(
            in NativeList<EventFrame> eventFrames,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            out ushort generation
        )
        {
            generation = ushort.MaxValue;

            bool found = false;

            for (int i = 0; i < eventFrames.Length; i++)
            {
                EventFrame frame = eventFrames[i];

                if (frame.Completed)
                    continue;
                if (frame.PendingVMContinuations > 0)
                    continue;

                ushort candidateGeneration = frame.Event.StructuralData.Generation;

                if (!found || candidateGeneration < generation)
                {
                    generation = candidateGeneration;
                    found = true;
                }
            }

            for (int i = 0; i < executionRequestQueue.Length; i++)
            {
                BehaviourExecutionRequest request = executionRequestQueue[i];

                if (IsExecutionRequestBlocked(in eventFrames, in request))
                {
                    continue;
                }

                ushort candidateGeneration = request.EmissionContext.StructuralData.Generation;

                if (!found || candidateGeneration < generation)
                {
                    generation = candidateGeneration;
                    found = true;
                }
            }

            return found;
        }

        private int FindTopFrameIndexForGeneration(
            in NativeList<EventFrame> eventFrames,
            ushort generation
        )
        {
            for (int i = eventFrames.Length - 1; i >= 0; i--)
            {
                EventFrame frame = eventFrames[i];

                if (frame.Completed)
                    continue;
                if (frame.PendingVMContinuations > 0)
                    continue;
                if (frame.Event.StructuralData.Generation != generation)
                    continue;

                return i;
            }

            return -1;
        }

        private int FindBestExecutionRequestIndex(
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            in NativeList<EventFrame> eventFrames,
            ushort generation
        )
        {
            int bestIndex = -1;

            BehaviourExecutionComparer comparer = new BehaviourExecutionComparer();

            for (int i = 0; i < executionRequestQueue.Length; i++)
            {
                BehaviourExecutionRequest candidate = executionRequestQueue[i];

                if (IsExecutionRequestBlocked(in eventFrames, in candidate))
                {
                    continue;
                }

                if (candidate.EmissionContext.StructuralData.Generation != generation)
                {
                    continue;
                }

                if (bestIndex < 0)
                {
                    bestIndex = i;
                    continue;
                }

                BehaviourExecutionRequest currentBest = executionRequestQueue[bestIndex];

                if (comparer.Compare(candidate, currentBest) > 0)
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void ExecuteBehaviourRequest(
            BehaviourExecutionRequest request,
            ref NativeList<EventFrame> eventFrames,
            ref NativeList<AbilityExecutionContinuation> continuations,
            ref BattleContext ctx
        )
        {
            int parentFrameIndex = FindFrameIndexByID(
                in eventFrames,
                request.EmissionContext.CurrentFrameID
            );

            if (parentFrameIndex < 0)
            {
                Logging.Warn(
                    LogCategory.Event,
                    $"Behaviour request could not find parent Frame "
                        + $"{request.EmissionContext.CurrentFrameID}."
                );

                return;
            }

            if (!behaviourStateLookup.HasBuffer(request.Owner))
            {
                Logging.Warn(LogCategory.Event, "Missing BehaviourRuntimeState buffer.");

                return;
            }

            DynamicBuffer<BehaviourRuntimeState> stateBuffer = behaviourStateLookup[request.Owner];

            ref EventFrame parentFrame = ref eventFrames.ElementAt(parentFrameIndex);

            AbilityExecutionResult result = BehaviourExecutor.Execute(
                request,
                ref parentFrame.Event,
                ref ctx,
                stateBuffer,
                out AbilityExecutionFrame executionFrame
            );

            if (result.Status == AbilityExecutionStatus.Yielded)
            {
                parentFrame.PendingVMContinuations++;

                Logging.Info(
                    LogCategory.Event,
                    $"[VM SUSPEND] "
                        + $"ParentFrame={parentFrame.ID} | "
                        + $"ParentOperation={parentFrame.Event.ExecutionData.OperationID} | "
                        + $"NewOperation={result.WaitingOperationID}"
                );

                continuations.Add(
                    new AbilityExecutionContinuation
                    {
                        Frame = executionFrame,
                        BehaviourStateIndex = request.RegistrationIndex,
                        BaseEmissionContext = request.EmissionContext,
                        ParentFrameID = parentFrame.ID,
                        WaitingOperationID = result.WaitingOperationID,
                    }
                );
            }
        }

        private int FindFrameIndexByID(in NativeList<EventFrame> eventFrames, uint frameID)
        {
            for (int i = eventFrames.Length - 1; i >= 0; i--)
            {
                if (eventFrames[i].ID == frameID)
                    return i;
            }

            return -1;
        }

        private void ProcessFrameStep(
            ref SystemState state,
            ref BattleContext ctx,
            ref EventFrame frame,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue
        )
        {
            switch (frame.Phase)
            {
                case BattleEventPhase.PreResolution:
                {
                    if (!frame.PhaseStarted)
                    {
                        BuildExecutionRequests(
                            ref state,
                            ref ctx,
                            frame.Event,
                            frame.ID,
                            frame.Phase,
                            executionRequestQueue
                        );

                        frame.PhaseStarted = true;

                        return;
                    }

                    frame.Phase = BattleEventPhase.Resolution;

                    frame.PhaseStarted = false;

                    return;
                }

                case BattleEventPhase.Resolution:
                {
                    if (!frame.PhaseStarted)
                    {
                        EventEmissionContext emissionContext = new EventEmissionContext
                        {
                            StructuralData = frame.Event.StructuralData,
                            ActionData = frame.Event.ActionData,
                            ExecutionData = frame.Event.ExecutionData,
                            CurrentFrameID = frame.ID,
                        };

                        BattleEventResolver.Resolve(frame.Event, ref ctx, in emissionContext);

                        frame.PhaseStarted = true;

                        return;
                    }

                    frame.Phase = BattleEventPhase.PostResolution;

                    frame.PhaseStarted = false;

                    return;
                }

                case BattleEventPhase.PostResolution:
                {
                    if (!frame.PhaseStarted)
                    {
                        BuildExecutionRequests(
                            ref state,
                            ref ctx,
                            frame.Event,
                            frame.ID,
                            frame.Phase,
                            executionRequestQueue
                        );

                        frame.PhaseStarted = true;

                        return;
                    }

                    frame.Completed = true;

                    return;
                }
            }
        }

        private void BuildExecutionRequests(
            ref SystemState state,
            ref BattleContext ctx,
            BattleEvent evt,
            uint currentFrameID,
            BattleEventPhase phase,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue
        )
        {
            NativeList<BehaviourExecutionRequest> executionList =
                new NativeList<BehaviourExecutionRequest>(Allocator.Temp);

            foreach (
                var (behaviours, entity) in SystemAPI
                    .Query<DynamicBuffer<BehaviourReference>>()
                    .WithEntityAccess()
            )
            {
                TriggerCollector.CollectFromEntity(
                    entity,
                    behaviours,
                    ref ctx,
                    evt,
                    currentFrameID,
                    phase,
                    ref executionList
                );
            }

            executionList.Sort(new BehaviourExecutionComparer());

            for (int i = 0; i < executionList.Length; i++)
            {
                executionRequestQueue.Add(executionList[i]);
            }

            executionList.Dispose();
        }

        private bool HasPendingCausalOperationWork(
            uint operationID,
            in NativeList<EventFrame> eventFrames,
            DynamicBuffer<ChainedBattleEvent> chainedEventQueue,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            in NativeList<AbilityExecutionContinuation> continuations
        )
        {
            if (operationID == EventExecutionData.InvalidOperationID)
            {
                return false;
            }

            for (int i = 0; i < eventFrames.Length; i++)
            {
                EventFrame frame = eventFrames[i];

                if (frame.Completed)
                    continue;

                if (frame.Event.ExecutionData.OperationID == operationID)
                {
                    return true;
                }
            }

            for (int i = 0; i < continuations.Length; i++)
            {
                AbilityExecutionContinuation continuation = continuations[i];

                uint parentOperationID = continuation.BaseEmissionContext.ExecutionData.OperationID;

                if (parentOperationID == operationID)
                {
                    return true;
                }
            }

            for (int i = 0; i < chainedEventQueue.Length; i++)
            {
                if (chainedEventQueue[i].Event.ExecutionData.OperationID == operationID)
                {
                    return true;
                }
            }

            for (int i = 0; i < executionRequestQueue.Length; i++)
            {
                if (
                    executionRequestQueue[i].EmissionContext.ExecutionData.OperationID
                    == operationID
                )
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPendingActionWork(
            uint actionExecutionID,
            in NativeList<EventFrame> eventFrames,
            DynamicBuffer<ChainedBattleEvent> chainedEventQueue,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            in NativeList<AbilityExecutionContinuation> continuations
        )
        {
            if (actionExecutionID == EventActionData.InvalidExecutionID)
            {
                return false;
            }

            for (int i = 0; i < eventFrames.Length; i++)
            {
                EventFrame frame = eventFrames[i];

                if (frame.Completed)
                    continue;

                if (frame.Event.ActionData.ActionExecutionID == actionExecutionID)
                {
                    return true;
                }
            }

            for (int i = 0; i < continuations.Length; i++)
            {
                AbilityExecutionContinuation continuation = continuations[i];

                if (
                    continuation.BaseEmissionContext.ActionData.ActionExecutionID
                    == actionExecutionID
                )
                {
                    return true;
                }
            }

            for (int i = 0; i < chainedEventQueue.Length; i++)
            {
                if (chainedEventQueue[i].Event.ActionData.ActionExecutionID == actionExecutionID)
                {
                    return true;
                }
            }

            for (int i = 0; i < executionRequestQueue.Length; i++)
            {
                if (
                    executionRequestQueue[i].EmissionContext.ActionData.ActionExecutionID
                    == actionExecutionID
                )
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResumeReadyContinuation(
            ref NativeList<AbilityExecutionContinuation> continuations,
            ref NativeList<EventFrame> eventFrames,
            DynamicBuffer<ChainedBattleEvent> chainedEventQueue,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            ref BattleContext ctx
        )
        {
            for (int i = continuations.Length - 1; i >= 0; i--)
            {
                AbilityExecutionContinuation continuation = continuations[i];

                if (
                    HasPendingCausalOperationWork(
                        continuation.WaitingOperationID,
                        in eventFrames,
                        chainedEventQueue,
                        executionRequestQueue,
                        in continuations
                    )
                )
                {
                    continue;
                }

                int parentFrameIndex = FindFrameIndexByID(
                    in eventFrames,
                    continuation.ParentFrameID
                );

                if (parentFrameIndex < 0)
                {
                    Logging.Warn(
                        LogCategory.Event,
                        $"[Scheduler] VM continuation could not "
                            + $"find parent Frame "
                            + $"{continuation.ParentFrameID}."
                    );

                    continuations.RemoveAt(i);
                    return true;
                }

                ref EventFrame parentFrame = ref eventFrames.ElementAt(parentFrameIndex);

                Entity behaviourOwner = continuation.Frame.BehaviourOwner;

                if (!behaviourStateLookup.HasBuffer(behaviourOwner))
                {
                    Logging.Warn(
                        LogCategory.Event,
                        "[Scheduler] Resuming VM is missing " + "BehaviourRuntimeState buffer."
                    );

                    UnblockVMContinuation(ref parentFrame);

                    continuations.RemoveAt(i);

                    return true;
                }

                DynamicBuffer<BehaviourRuntimeState> stateBuffer = behaviourStateLookup[
                    behaviourOwner
                ];

                Logging.Info(
                    LogCategory.Event,
                    $"[VM RESUME] "
                        + $"ParentFrame={continuation.ParentFrameID} | "
                        + $"CompletedOperation={continuation.WaitingOperationID} | "
                        + $"ResumeIP={continuation.Frame.InstructionPointer}"
                );

                AbilityExecutionResult result = BehaviourExecutor.Resume(
                    ref continuation,
                    ref parentFrame.Event,
                    ref ctx,
                    stateBuffer
                );

                if (result.Status == AbilityExecutionStatus.Yielded)
                {
                    Logging.Info(
                        LogCategory.Event,
                        $"[VM RE-YIELD] "
                            + $"NewOperation={result.WaitingOperationID} | "
                            + $"ResumeIP={continuation.Frame.InstructionPointer}"
                    );

                    continuation.WaitingOperationID = result.WaitingOperationID;

                    continuations[i] = continuation;

                    return true;
                }
                // Completed OR Aborted.
                UnblockVMContinuation(ref parentFrame);

                Logging.Info(
                    LogCategory.Event,
                    $"[VM COMPLETE] "
                        + $"ParentFrame={continuation.ParentFrameID} | "
                        + $"Program={continuation.Frame.ProgramIndex}"
                );

                continuations.RemoveAt(i);

                return true;
            }

            return false;
        }

        private void EmitCompletedActionFacts(
            ref BattleContext ctx,
            in NativeList<EventFrame> eventFrames,
            DynamicBuffer<ChainedBattleEvent> chainedEventQueue,
            DynamicBuffer<BehaviourExecutionRequest> executionRequestQueue,
            in NativeList<AbilityExecutionContinuation> continuations
        )
        {
            for (int i = ctx.ActionExecutionStates.Length - 1; i >= 0; i--)
            {
                ActionExecutionState actionState = ctx.ActionExecutionStates[i];

                if (
                    HasPendingActionWork(
                        actionState.ActionExecutionID,
                        in eventFrames,
                        chainedEventQueue,
                        executionRequestQueue,
                        in continuations
                    )
                )
                {
                    continue;
                }

                uint sourceRuntimeID = ctx.CardRuntimeIDLookup[actionState.Source].Value;

                uint targetRuntimeID = ctx.CardRuntimeIDLookup[actionState.PrimaryTarget].Value;

                PresentationFactContext factContext = new PresentationFactContext
                {
                    BattleRuntimeID = ctx.BattleID,

                    SourceRuntimeID = sourceRuntimeID,
                    TargetRuntimeID = targetRuntimeID,

                    ActionDefinitionID = 0,

                    ActionExecutionID = actionState.ActionExecutionID,

                    ActionResultIndex = PresentationFactMetadata.NoActionResult,

                    GroupID = EventStructuralData.InvalidGroupID,

                    Generation = 0,
                };

                PresentationFactEmitter.EmitActionCompletedFact(
                    factContext,
                    ctx.PresentationFactQueue,
                    ctx.PresentationSequenceCounter
                );

                Logging.Info(
                    LogCategory.Simulation,
                    $"Action Completed: "
                        + $"Execution={actionState.ActionExecutionID} | "
                        + $"Type={actionState.ActionType} | "
                        + $"Source={actionState.Source.Index}"
                );

                ctx.ActionExecutionStates.RemoveAt(i);
            }
        }

        private bool IsExecutionRequestBlocked(
            in NativeList<EventFrame> eventFrames,
            in BehaviourExecutionRequest request
        )
        {
            int parentIndex = FindFrameIndexByID(
                in eventFrames,
                request.EmissionContext.CurrentFrameID
            );

            if (parentIndex < 0)
                return false;

            return eventFrames[parentIndex].PendingVMContinuations > 0;
        }

        private void UnblockVMContinuation(ref EventFrame parentFrame)
        {
            if (parentFrame.PendingVMContinuations == 0)
            {
                Logging.Warn(
                    LogCategory.Event,
                    $"[Scheduler] Frame {parentFrame.ID} "
                        + "attempted to release a VM continuation "
                        + "but none were registered."
                );

                return;
            }

            parentFrame.PendingVMContinuations--;
        }

        private uint RetrieveNextFrameID(RefRW<BattleEventFrameIDCounter> counter)
        {
            uint nextID = counter.ValueRO.NextID;

            counter.ValueRW.NextID++;

            return nextID;
        }

        private void LogFrameCreated(string origin, in EventFrame frame)
        {
            EventStructuralData data = frame.Event.StructuralData;

            Logging.Info(
                LogCategory.Event,
                $"[{origin}] "
                    + $"Frame={frame.ID} | "
                    + $"Parent={data.ParentFrameID} | "
                    + $"Group={data.GroupID} | "
                    + $"Gen={data.Generation} | "
                    + $"Type={frame.Event.Type} | "
                    + $"Source={frame.Event.Source.Index} | "
                    + $"Target={frame.Event.Target.Index}"
            );
        }

        private void LogFrameResolution(in EventFrame frame)
        {
            EventStructuralData data = frame.Event.StructuralData;

            Logging.Info(
                LogCategory.Event,
                $"[RESOLVE] "
                    + $"Frame={frame.ID} | "
                    + $"Parent={data.ParentFrameID} | "
                    + $"Group={data.GroupID} | "
                    + $"Gen={data.Generation} | "
                    + $"Type={frame.Event.Type}"
            );
        }

        private void ValidateFrame(in EventFrame frame)
        {
            if (!frame.Event.StructuralData.HasStructuralData)
            {
                Logging.Warn(
                    LogCategory.Event,
                    $"[EVENT STRUCTURE] Frame "
                        + $"{frame.ID} "
                        + $"({frame.Event.Type}) has no valid "
                        + $"structural data."
                );
            }
        }
    }
}
