namespace Archeus.Battle.Events.Payloads
{
    public struct EffectPayload
    {
        public int EffectIndex;
        public float Strength;
        public bool IsPermanent;
        public int Duration;
    }
}
