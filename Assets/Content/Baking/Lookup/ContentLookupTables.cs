using Unity.Collections;
using Unity.Entities;

namespace Archeus.Content.Lookup
{
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
}

