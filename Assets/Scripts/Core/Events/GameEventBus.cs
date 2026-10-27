using System;
using System.Collections.Generic;

namespace MassFactory.Core
{
    /// <summary>
    /// Minimal typed publish/subscribe bus. Each <see cref="GameSession"/> owns its own bus, so
    /// starting a new run never leaves stale subscribers attached to the old one.
    /// </summary>
    public sealed class GameEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler != null && _handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            // Copy so handlers may subscribe/unsubscribe or publish nested events safely.
            var snapshot = list.ToArray();
            foreach (var handler in snapshot)
                ((Action<T>)handler)(evt);
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
