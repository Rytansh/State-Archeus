using System;
using Archeus.Battle.Buffers.Actions;
using Archeus.Battle.Events.Context;
using Unity.Entities;

namespace Archeus.Battle.Runtime
{
    public static class ActionResultGroupAllocator
    {
        public static ushort Allocate(
            uint actionExecutionID,
            DynamicBuffer<ActionExecutionState> actionStates
        )
        {
            if (actionExecutionID == EventActionData.InvalidExecutionID)
            {
                return EventActionData.NoActionResultGroup;
            }

            for (int i = 0; i < actionStates.Length; i++)
            {
                ActionExecutionState runtimeState = actionStates[i];

                if (runtimeState.ActionExecutionID != actionExecutionID)
                    continue;

                if (runtimeState.NextResultGroupIndex == EventActionData.NoActionResultGroup)
                {
                    throw new InvalidOperationException(
                        $"Action {actionExecutionID} exceeded "
                            + $"the available result index range."
                    );
                }

                ushort resultIndex = runtimeState.NextResultGroupIndex;

                runtimeState.NextResultGroupIndex++;

                actionStates[i] = runtimeState;

                return resultIndex;
            }

            throw new InvalidOperationException(
                $"No runtime state exists for Action " + $"{actionExecutionID}."
            );
        }
    }
}
