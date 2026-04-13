namespace Archeus.Game.Bootstrap
{
    public interface IRNGProvider
    {
        public DeterministicRNG GetRNG(string key, ulong salt = 0);
    }
}
