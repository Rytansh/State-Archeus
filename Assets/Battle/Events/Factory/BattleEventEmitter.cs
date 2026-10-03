using Archeus.Battle.Buffers.Events;
using Archeus.Battle.Components.Core;
using Archeus.Battle.Events.Context;
using Unity.Entities;

namespace Archeus.Battle.Events.Factory
{
    public static class BattleEventEmitter
    {
        public static void EmitOriginEvent(
            BattleEvent evt,
            ref DynamicBuffer<BattleEvent> mainEventQueue,
            RefRW<BattleEventGroupIDCounter> groupCounter
        )
        {
            evt.StructuralData = new EventStructuralData
            {
                GroupID = RetrieveNextGroupID(groupCounter),
                Generation = 0,
                ParentFrameID = EventStructuralData.InvalidParentFrameID,
            };

            mainEventQueue.Add(evt);
        }

        public static void EmitContinuationEvent(
            BattleEvent evt,
            ref DynamicBuffer<ChainedBattleEvent> queue,
            in EventEmissionContext context
        )
        {
            evt.StructuralData = new EventStructuralData
            {
                GroupID = context.StructuralData.GroupID,
                Generation = context.StructuralData.Generation,
                ParentFrameID = context.CurrentFrameID,
            };
            evt.ActionData = context.ActionData;
            evt.ExecutionData = context.ExecutionData;

            queue.Add(new ChainedBattleEvent { Event = evt });
        }

        public static void EmitConsequenceEvent(
            BattleEvent evt,
            ref DynamicBuffer<ChainedBattleEvent> queue,
            in EventEmissionContext context
        )
        {
            evt.StructuralData = new EventStructuralData
            {
                GroupID = context.StructuralData.GroupID,
                Generation = checked((ushort)(context.StructuralData.Generation + 1)),
                ParentFrameID = context.CurrentFrameID,
            };
            evt.ActionData = context.ActionData;
            evt.ExecutionData = context.ExecutionData;

            queue.Add(new ChainedBattleEvent { Event = evt });
        }

        private static uint RetrieveNextGroupID(RefRW<BattleEventGroupIDCounter> groupCounter)
        {
            uint nextID = groupCounter.ValueRO.NextID;

            groupCounter.ValueRW.NextID++;

            return nextID;
        }
    }
}
