using Unity.Entities;
public struct ContentBlobRegistry
{
    public BlobArray<CharacterDefinitionBlob> Characters;
    public BlobArray<SkillDefinitionBlob> Skills;
    public BlobArray<BehaviourDefinitionBlob> Behaviours;
}

public struct ContentBlobRegistryComponent : IComponentData
{
    public BlobAssetReference<ContentBlobRegistry> BlobRegistryReference;
}



