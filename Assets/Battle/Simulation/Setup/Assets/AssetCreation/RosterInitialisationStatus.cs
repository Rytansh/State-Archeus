using Unity.Entities;

public struct RosterReadyRequest : IComponentData
{
    public Entity Battle;
}

public struct RosterInitialisationFailed : IComponentData
{
    public Entity Battle;
}
