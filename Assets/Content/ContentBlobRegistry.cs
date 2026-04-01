using DBUS.Battle.VM.Data;
using Unity.Entities;
public struct ContentBlobRegistry
{
    public BlobArray<CharacterDefinitionBlob> Characters;
    public BlobArray<SkillDefinitionBlob> Skills;
    public BlobArray<BehaviourDefinitionBlob> Behaviours;
    public BlobArray<AbilityProgram> AbilityPrograms;
}

public struct ContentBlobRegistryComponent : IComponentData
{
    public BlobAssetReference<ContentBlobRegistry> BlobRegistryReference;
}



