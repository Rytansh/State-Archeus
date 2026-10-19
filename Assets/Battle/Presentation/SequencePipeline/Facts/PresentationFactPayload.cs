namespace Archeus.Battle.Presentation.Facts
{
    public struct PresentationFactPayload
    {
        public PresentationHitPayload HitPayload;
    }

    public struct PresentationHitPayload
    {
        public float Damage;
        public bool IsCrit;
    }
}
