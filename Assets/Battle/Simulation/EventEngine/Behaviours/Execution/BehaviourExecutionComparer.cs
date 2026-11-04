using System.Collections.Generic;
using Archeus.Battle.Buffers.VM;

namespace Archeus.Battle.VM.Execution
{
    public struct BehaviourExecutionComparer : IComparer<BehaviourExecutionRequest>
    {
        public int Compare(BehaviourExecutionRequest a, BehaviourExecutionRequest b)
        {
            // COMPARE PRIORITY
            int priorityCompare = b.Priority.CompareTo(a.Priority);
            if (priorityCompare != 0)
                return priorityCompare;

            // Raw Entity indices can change when rollback recreates an entity. Card runtime
            // identity survives restoration and therefore must determine passive ordering.
            int ownerCompare = a.OwnerRuntimeID.CompareTo(b.OwnerRuntimeID);
            if (ownerCompare != 0)
                return ownerCompare;

            // COMPARE REGISTRATIONINDEX
            return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
        }
    }
}
