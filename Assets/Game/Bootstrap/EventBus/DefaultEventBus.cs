using System;
using System.Collections.Generic;

namespace Archeus.Game.Bootstrap
{
    public class DefaultEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            if (!subscribers.TryGetValue(typeof(T), out var list))
                list = subscribers[typeof(T)] = new List<Delegate>();

            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (subscribers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Raise<T>(in T evt)
        {
            if (subscribers.TryGetValue(typeof(T), out var list))
            {
                foreach (var sub in list)
                    (sub as Action<T>)?.Invoke(evt);
            }
        }
    }
}

