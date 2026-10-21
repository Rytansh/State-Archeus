using Unity.Entities;

public struct BattlePlayerReference : IBufferElementData
{
    public Entity Player;
}

public struct BattlePlayerIdentity : IComponentData
{
    public ulong PlayerID;
}
