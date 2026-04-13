using System;
using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public sealed class WorldContext
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T instance) where T : class
        {
            _services[typeof(T)] = instance;
        }

        public T Resolve<T>() where T : class
        {
            return (T)_services[typeof(T)];
        }

        public bool TryResolve<T>(out T instance) where T : class
        {
            if (_services.TryGetValue(typeof(T), out var obj))
            {
                instance = (T)obj;
                return true;
            }

            instance = null;
            return false;
        }
    }
}

