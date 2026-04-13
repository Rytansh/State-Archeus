namespace Archeus.Game.Bootstrap
{
    public interface IBootstrapProcess
    {
        int Order {get;}
        void Initialise(WorldContext context);
    }
}
