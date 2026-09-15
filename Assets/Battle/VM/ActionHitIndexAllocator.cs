using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Events.Context;
using Unity.Entities;

public static class ActionHitIndexAllocator
{
    public static ushort Allocate(
        uint actionExecutionID,
        DynamicBuffer<ActionExecutionState> actionStates
    )
    {
        for (int i = 0; i < actionStates.Length; i++)
        {
            ActionExecutionState state = actionStates[i];

            if (state.ActionExecutionID != actionExecutionID)
            {
                continue;
            }

            ushort hitIndex = state.NextHitIndex;

            if (hitIndex == EventActionData.NoHit)
            {
                // Defensive overflow handling.
                return EventActionData.NoHit;
            }

            state.NextHitIndex++;

            actionStates[i] = state;

            return hitIndex;
        }

        return EventActionData.NoHit;
    }
}
