using Archeus.Battle.Components.Ownership;
using Unity.Entities;

public struct DeckCard : IBufferElementData
{
    public Entity Card;
}

public struct HandCard : IBufferElementData
{
    public Entity Card;
    public HandPosition Position;
}

public enum HandPosition : byte
{
    Slot1,
    Slot2,
    Slot3,
    Slot4,
}

public struct FieldCard : IBufferElementData
{
    public Entity Card;
    public FieldPosition Position;
}

public struct BattleDeckEntry : IBufferElementData
{
    public uint CardDefinitionID;
    public RuntimeCardType CardType;
}

public enum FieldPosition : byte
{
    Lead,
    AttackingForceSlot1,
    AttackingForceSlot2,
    AttackingForceSlot3,
    SkillSlot1,
    SkillSlot2,
    SkillSlot3,
    SkillSlot4,
}
