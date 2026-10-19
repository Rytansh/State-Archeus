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

            // COMPARE OWNER ENTITY INDEX
            int ownerCompare = a.Owner.Index.CompareTo(b.Owner.Index);
            if (ownerCompare != 0)
                return ownerCompare;

            // COMPARE REGISTRATIONINDEX
            return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
        }
    }
}
