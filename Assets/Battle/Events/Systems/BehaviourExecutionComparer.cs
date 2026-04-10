using Unity.Entities;
using System.Collections.Generic;
using Unity.Collections;
using DBUS.Battle.Components.Events;

public struct BehaviourExecutionComparer : IComparer<BehaviourExecutionRequest>
{
    public int Compare(BehaviourExecutionRequest a, BehaviourExecutionRequest b)
    {
        // 1. Priority (higher first)
        int priorityCompare = b.Priority.CompareTo(a.Priority);
        if (priorityCompare != 0)
            return priorityCompare;

        // 2. Owner entity index (stable global ordering)
        int ownerCompare = a.Owner.Index.CompareTo(b.Owner.Index);
        if (ownerCompare != 0)
            return ownerCompare;

        // 3. Registration index (preserve behaviour order within entity)
        return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
    }
}
