namespace Archeus.Battle.Events.Payloads
{
    public struct DamagePayload
    {
        public float AttackMultiplier;
        public float BaseDamage;
        public float FinalDamage;

        public DamageSnapshot Snapshot;
    }

    public struct DamageSnapshot
    {
        public float AttackerAttack;
        public float TargetDefense;
        public float TargetCurrentHealth;
        public float TargetMaxHealth;
    }
}
