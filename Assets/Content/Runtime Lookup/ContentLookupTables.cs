using Unity.Collections;
using Unity.Entities;
public struct ContentLookupTables : IComponentData, System.IDisposable
{
    public NativeHashMap<uint, int> CharacterIDToIndex;
    public NativeHashMap<uint, int> SkillIDToIndex;
    public NativeHashMap<uint, int> BehaviourIDToIndex;

    public void Dispose()
    {
        if (CharacterIDToIndex.IsCreated) CharacterIDToIndex.Dispose();
        if (SkillIDToIndex.IsCreated) SkillIDToIndex.Dispose();
        if (BehaviourIDToIndex.IsCreated) BehaviourIDToIndex.Dispose();
    }
}

