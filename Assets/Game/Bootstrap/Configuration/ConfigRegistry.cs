using System;
using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public class ConfigRegistry : IConfigRegistry
    {
        private readonly Dictionary<Type, object> _configs = new();

        public void Register<T>(T config) where T : class
        {
            _configs[typeof(T)] = config;
        }

        public T Get<T>() where T : class
        {
            if (_configs.TryGetValue(typeof(T), out var value))
                return (T)value;

            throw new KeyNotFoundException($"Config of type {typeof(T).Name} not found.");
        }

        public bool TryGet<T>(out T config) where T : class
        {
            if (_configs.TryGetValue(typeof(T), out var value))
            {
                config = (T)value;
                return true;
            }

            config = default;
            return false;
        }
    }
}
