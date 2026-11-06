namespace Archeus.Battle.Presentation.Facts
{
    public struct PresentationFactPayload
    {
        public PresentationHitPayload HitPayload;
        public Archeus.Battle.Presentation.State.SchedulerFactPayload Scheduler;
    }

    public struct PresentationHitPayload
    {
        public float Damage;
        public bool IsCrit;
    }
}
