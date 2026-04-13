using Unity.Entities;

namespace Archeus.Battle.Components.Turns
{
    public struct TurnCounter : IComponentData
    {
        public int CurrentTurn;
    }
    
}