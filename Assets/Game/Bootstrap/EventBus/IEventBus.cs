using System;

namespace Archeus.Game.Bootstrap
{
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Raise<T>(in T evt);
    }
}

