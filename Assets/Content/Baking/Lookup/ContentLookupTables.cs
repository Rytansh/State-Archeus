using Unity.Collections;
using Unity.Entities;

namespace Archeus.Content.Lookup
{
    public struct ContentLookupTables : IComponentData, System.IDisposable
    {
        public NativeHashMap<uint, int> CharacterIDToIndex;
        public NativeHashMap<uint, int> SkillIDToIndex;
        public NativeHashMap<uint, int> BehaviourIDToIndex;
        public NativeHashMap<uint, int> EffectIDToIndex;

        public void Dispose()
        {
            if (CharacterIDToIndex.IsCreated) CharacterIDToIndex.Dispose();
            if (SkillIDToIndex.IsCreated) SkillIDToIndex.Dispose();
            if (BehaviourIDToIndex.IsCreated) BehaviourIDToIndex.Dispose();
            if (EffectIDToIndex.IsCreated) EffectIDToIndex.Dispose();
        }
    }
}

