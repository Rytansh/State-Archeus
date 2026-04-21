using Unity.Entities;
using Archeus.Content.Blobs;

namespace Archeus.Content.Registries
{
    public struct ContentBlobRegistry
    {
        public BlobArray<CharacterDefinitionBlob> Characters;
        public BlobArray<SkillDefinitionBlob> Skills;
        public BlobArray<BehaviourDefinitionBlob> Behaviours;
        public BlobArray<AbilityProgram> AbilityPrograms;
        public BlobArray<EffectDefinitionBlob> Effects;
    }

    public struct ContentBlobRegistryComponent : IComponentData
    {
        public BlobAssetReference<ContentBlobRegistry> BlobRegistryReference;
    }
}



