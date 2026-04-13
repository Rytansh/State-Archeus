using Unity.Entities;

namespace Archeus.Battle.Components.Combat
{
    public struct MaxSuperchargedEnergy: IComponentData
    {
        public int Value;
    }
    // holds the maximum supercharged energy a character can have.

    public struct HeldSuperchargedEnergy: IComponentData
    {
        public int Value;
    }
    // holds the currently held supercharged energy in a turn.
}
