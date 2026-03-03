using Unity.Entities;

namespace DBUS.Battle.Components.Turns
{
    public struct TurnCounter : IComponentData
    {
        public int CurrentTurn;
    }
    
}