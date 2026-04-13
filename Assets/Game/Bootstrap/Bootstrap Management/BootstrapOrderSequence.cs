namespace Archeus.Game.Bootstrap
{
    public static class SharedBootstrapOrder
    {
        public const int Logging = 0;
        public const int Seeding = 100;
        public const int RNG = 200;
        public const int RNGTests = 210;
    }

    public static class SimulationBootstrapOrder
    {
        public const int Config = 0;
        public const int SimulationWorld = 100;
        public const int EventBus = 200;
    }

    public static class PresentationBootstrapOrder {
        public const int PresentationWorld = 0;
    }
}
