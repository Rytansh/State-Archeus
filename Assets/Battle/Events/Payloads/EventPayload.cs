namespace Archeus.Battle.Events.Payloads
{
    public struct EventPayload
    {
        public FlowPayload Flow;
        public ResourcePayload Resource;
        public DamagePayload Damage;
        public StatusPayload Status;
        public CombatPayload Combat;
    }
}
