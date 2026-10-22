using Archeus.Battle.Components.Ownership;
using Unity.Entities;

public struct CardEntityComponents
{
    public Entity Battle;
    public Entity Owner;
    public uint DefinitionID;
    public uint RuntimeID;
    public BattleSideAssignment SideAssignment;
}
