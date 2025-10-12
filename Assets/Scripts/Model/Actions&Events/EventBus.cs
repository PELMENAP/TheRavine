using System.Collections.Generic;
using System;

using TheRavine.EntityControl;

namespace TheRavine.Events
{
    public class EventBus
    {
        private readonly Dictionary<Type, Delegate> eventTable = new();

        public void Subscribe<T>(Action<AEntity, T> listener) where T : IGameEvent
        {
            if (eventTable.TryGetValue(typeof(T), out var existing))
                eventTable[typeof(T)] = (Action<AEntity, T>)existing + listener;
            else
                eventTable[typeof(T)] = listener;
        }

        public void Unsubscribe<T>(Action<AEntity, T> listener) where T : IGameEvent
        {
            if (!eventTable.TryGetValue(typeof(T), out var existing)) return;
            eventTable[typeof(T)] = (Action<AEntity, T>)existing - listener;

            if (eventTable[typeof(T)] == null)
                eventTable.Remove(typeof(T));
        }

        public void Invoke<T>(AEntity sender, T ev) where T : IGameEvent
        {
            if (eventTable.TryGetValue(typeof(T), out var d))
                ((Action<AEntity, T>)d)?.Invoke(sender, ev);
        }

        public void Clear() => eventTable.Clear();
    }
}