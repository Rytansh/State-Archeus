namespace Archeus.Battle.Events.Payloads
{
    public struct DamagePayload
    {
        public float AttackMultiplier;
        public float CritMultiplier;
        public float BaseDamage;
        public float FinalDamage;

        public bool DidCrit;
    }
}
