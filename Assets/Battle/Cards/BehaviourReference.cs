using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;

public struct BehaviourReference : IBufferElementData
{
    public int BehaviourIndex;
}

public struct BehaviourRuntimeState : IBufferElementData
{
    public FixedList64Bytes<float> Memory;
}
