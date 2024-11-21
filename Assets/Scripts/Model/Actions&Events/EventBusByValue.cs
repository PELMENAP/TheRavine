using System.Collections.Generic;
using System;

namespace TheRavine.Events
{
    public class EventBusByValue
    {
        private Dictionary<Type, List<object>> eventHandlers;
        public EventBusByValue()
        {
            eventHandlers = new Dictionary<Type, List<object>>();
        }
        public void Subscribe<T>(IEntityObserver<T> observer)
        {
            Type eventType = typeof(T);
            if (!eventHandlers.ContainsKey(eventType))
            {
                eventHandlers[eventType] = new List<object>();
            }
            eventHandlers[eventType].Add(observer);
        }

        public void Unsubscribe<T>(IEntityObserver<T> observer)
        {
            Type eventType = typeof(T);
            if (eventHandlers.ContainsKey(eventType))
            {
                eventHandlers[eventType].Remove(observer);
            }
        }
        public void RaiseEvent<T>(IEntityEvent<T> entityEvent)
        {
            Type eventType = typeof(T);
            if (eventHandlers.ContainsKey(eventType))
            {
                foreach (var handler in eventHandlers[eventType])
                {
                    if (handler is IEntityObserver<T> observer)
                    {
                        observer.OnEvent(entityEvent);
                    }
                }
            }
        }
    }
}