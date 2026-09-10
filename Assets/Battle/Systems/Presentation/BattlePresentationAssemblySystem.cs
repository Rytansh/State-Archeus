using Archeus.Battle.Buffers.Presentation;
using Archeus.Battle.Components.Presentation;
using Archeus.Battle.Presentation.Facts;
using Archeus.Battle.Systems.Presentation;
using Archeus.Core.Debugging;
using Unity.Collections;
using Unity.Entities;

[DisableAutoCreation]
[UpdateInGroup(typeof(BattlePresentationGroup))]
[UpdateAfter(typeof(PresentationFactImportSystem))]
public partial struct BattlePresentationAssemblySystem : ISystem
{
    private NativeHashMap<PresentationActionKey, Entity> openActionPackets;

    private EntityQuery inboxQuery;

    public void OnCreate(ref SystemState state)
    {
        openActionPackets = new NativeHashMap<PresentationActionKey, Entity>(
            16,
            Allocator.Persistent
        );

        inboxQuery = state.GetEntityQuery(
            ComponentType.ReadOnly<BattlePresentationInboxTag>(),
            ComponentType.ReadWrite<PresentationFact>()
        );

        state.RequireForUpdate(inboxQuery);
    }

    public void OnDestroy(ref SystemState state)
    {
        if (openActionPackets.IsCreated)
        {
            openActionPackets.Dispose();
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        Entity inboxEntity = inboxQuery.GetSingletonEntity();

        DynamicBuffer<PresentationFact> factsQueue =
            state.EntityManager.GetBuffer<PresentationFact>(inboxEntity);

        if (factsQueue.Length == 0)
            return;

        NativeList<PresentationFact> facts = new NativeList<PresentationFact>(
            factsQueue.Length,
            Allocator.Temp
        );

        for (int i = 0; i < factsQueue.Length; i++)
        {
            facts.Add(factsQueue[i]);
        }

        factsQueue.Clear();

        for (int i = 0; i < facts.Length; i++)
        {
            PresentationFact fact = facts[i];
            ProcessFact(ref state, in fact);
        }

        facts.Dispose();
    }

    private void ProcessFact(ref SystemState state, in PresentationFact fact)
    {
        PresentationFactMetadata metadata = fact.FactMetadata;

        if (metadata.ActionExecutionID == PresentationFactMetadata.NoAction)
        {
            // standalone route later
            return;
        }

        PresentationActionKey key = new PresentationActionKey
        {
            BattleRuntimeID = metadata.BattleRuntimeID,

            ActionExecutionID = metadata.ActionExecutionID,
        };

        switch (fact.FactType)
        {
            case PresentationFactType.ActionStarted:
            {
                OpenActionPacket(ref state, in key, in fact);

                break;
            }

            case PresentationFactType.ActionCompleted:
            {
                SealActionPacket(ref state, in key, in fact);

                break;
            }

            default:
            {
                AppendFactToAction(ref state, in key, in fact);

                break;
            }
        }
    }

    private void OpenActionPacket(
        ref SystemState state,
        in PresentationActionKey key,
        in PresentationFact fact
    )
    {
        PresentationFactMetadata metadata = fact.FactMetadata;

        if (openActionPackets.ContainsKey(key))
        {
            return;
        }

        Entity packetEntity = state.EntityManager.CreateEntity();

        state.EntityManager.SetName(
            packetEntity,
            $"Presentation Action " + $"{metadata.ActionExecutionID}"
        );

        state.EntityManager.AddComponentData(
            packetEntity,
            new PresentationActionPacket
            {
                BattleRuntimeID = metadata.BattleRuntimeID,
                ActionExecutionID = metadata.ActionExecutionID,
                ActionDefinitionID = metadata.ActionDefinitionID,
                SourceRuntimeID = metadata.SourceRuntimeID,
                FirstSequence = metadata.Sequence,
                LastSequence = 0,
                Status = PresentationActionPacketStatus.Open,
            }
        );

        state.EntityManager.AddBuffer<PresentationAction>(packetEntity);

        if (!openActionPackets.TryAdd(key, packetEntity))
        {
            Logging.Warn(
                LogCategory.Presentation,
                $"Failed to register open action packet | Battle={metadata.BattleRuntimeID} | Action={metadata.ActionExecutionID}"
            );

            state.EntityManager.DestroyEntity(packetEntity);

            return;
        }

        Logging.Info(
            LogCategory.Presentation,
            $"Opened action | Battle={metadata.BattleRuntimeID} | Action={metadata.ActionExecutionID} | Seq={metadata.Sequence}"
        );
    }

    private void AppendFactToAction(
        ref SystemState state,
        in PresentationActionKey key,
        in PresentationFact fact
    )
    {
        PresentationFactMetadata metadata = fact.FactMetadata;

        if (!openActionPackets.TryGetValue(key, out Entity packetEntity))
        {
            Logging.Warn(
                LogCategory.Presentation,
                $"Action fact arrived "
                    + $"without an open packet | "
                    + $"Battle={metadata.BattleRuntimeID} | "
                    + $"Action={metadata.ActionExecutionID} | "
                    + $"Seq={metadata.Sequence} | "
                    + $"Type={fact.FactType}"
            );

            return;
        }

        if (!state.EntityManager.HasBuffer<PresentationAction>(packetEntity))
        {
            Logging.Warn(
                LogCategory.Presentation,
                $"Packet entity " + $"{packetEntity.Index} " + $"has no PresentationAction buffer."
            );

            return;
        }

        DynamicBuffer<PresentationAction> packetFacts =
            state.EntityManager.GetBuffer<PresentationAction>(packetEntity);

        packetFacts.Add(new PresentationAction { Fact = fact });

        Logging.Info(
            LogCategory.Presentation,
            $"Appended fact | "
                + $"Action={metadata.ActionExecutionID} | "
                + $"Seq={metadata.Sequence} | "
                + $"Type={fact.FactType} | "
                + $"Result="
                + FormatResult(metadata.ActionResultIndex)
        );
    }

    private void SealActionPacket(
        ref SystemState state,
        in PresentationActionKey key,
        in PresentationFact fact
    )
    {
        PresentationFactMetadata metadata = fact.FactMetadata;

        if (!openActionPackets.TryGetValue(key, out Entity packetEntity))
        {
            Logging.Warn(
                LogCategory.Presentation,
                $"ActionCompleted arrived "
                    + $"without an open packet | "
                    + $"Battle={metadata.BattleRuntimeID} | "
                    + $"Action={metadata.ActionExecutionID}"
            );

            return;
        }

        PresentationActionPacket packet =
            state.EntityManager.GetComponentData<PresentationActionPacket>(packetEntity);

        packet.LastSequence = metadata.Sequence;

        packet.Status = PresentationActionPacketStatus.Sealed;

        state.EntityManager.SetComponentData(packetEntity, packet);

        DynamicBuffer<PresentationAction> packetFacts =
            state.EntityManager.GetBuffer<PresentationAction>(packetEntity);

        openActionPackets.Remove(key);

        Logging.Info(
            LogCategory.Presentation,
            $"Sealed action | "
                + $"Battle={metadata.BattleRuntimeID} | "
                + $"Action={metadata.ActionExecutionID} | "
                + $"Facts={packetFacts.Length} | "
                + $"Sequence="
                + $"{packet.FirstSequence}"
                + $"..{packet.LastSequence}"
        );
    }

    private static string FormatResult(ushort actionResultIndex)
    {
        return actionResultIndex == PresentationFactMetadata.NoActionResult
            ? "None"
            : actionResultIndex.ToString();
    }
}
