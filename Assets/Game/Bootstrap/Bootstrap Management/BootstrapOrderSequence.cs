namespace Archeus.Game.Bootstrap
{
    public static class SharedBootstrapOrder
    {
        public const int Logging = 0;
        public const int Config = 50;
        public const int Seeding = 100;
        public const int RNGTests = 200;
        public const int EventBus = 300;
        public const int BattlePresentationBridge = 400;
    }

    public static class SimulationBootstrapOrder
    {
        public const int BattleSimulationWorld = 0;
    }

    public static class PresentationBootstrapOrder
    {
        public const int BattlePresentationWorld = 0;
    }
}
