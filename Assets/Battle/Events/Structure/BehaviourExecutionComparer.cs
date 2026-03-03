using Unity.Entities;
using System.Collections.Generic;
using Unity.Collections;
using DBUS.Battle.Components.Events;

public struct BehaviorExecutionComparer : IComparer<BehaviorExecutionRequest>
{
    public int Compare(BehaviorExecutionRequest a, BehaviorExecutionRequest b)
    {
        int priorityCompare = b.Priority.CompareTo(a.Priority);
        if (priorityCompare != 0)
            return priorityCompare;
        return a.RegistrationIndex.CompareTo(b.RegistrationIndex);
    }
}
