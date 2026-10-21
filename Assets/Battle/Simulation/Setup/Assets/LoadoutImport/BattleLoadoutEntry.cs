using Unity.Entities;

public struct BattlePlayerEntry : IBufferElementData
{
    public ulong PlayerID;
    public uint LeadCharacterDefinitionID;
}

public struct PlayerDeckEntry : IBufferElementData
{
    public ulong PlayerID;
    public uint CardDefinitionID;
    public RuntimeCardType CardType;
}
