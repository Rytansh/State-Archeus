
namespace Archeus.Game.Bootstrap
{
    public interface IConfigRegistry
    {
        void Register<T>(T config) where T : class;
        T Get<T>() where T : class;
        bool TryGet<T>(out T config) where T : class;
    }
}
