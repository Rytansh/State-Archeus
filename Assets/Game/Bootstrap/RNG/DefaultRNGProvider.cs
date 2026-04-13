using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public class DefaultRNGProvider : IRNGProvider
    {
        private readonly ISeedService seedService;
        private readonly Dictionary<string, DeterministicRNG> streams = new();

        public DefaultRNGProvider(ISeedService seedService)
        {
            this.seedService = seedService;
        }

        public DeterministicRNG GetRNG(string key, ulong salt = 0)
        {
            if (streams.TryGetValue(key, out var rng))
                return rng;

            rng = seedService.CreateRNG(key, salt);
            streams[key] = rng;
            return rng;
        }
    }
}
