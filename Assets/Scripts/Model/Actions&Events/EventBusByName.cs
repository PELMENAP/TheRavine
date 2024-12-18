using System.Collections.Generic;
using System;

namespace TheRavine.Events
{
    public class EventBusByName
    {
        private Dictionary<string, Delegate> eventDictionary = new Dictionary<string, Delegate>();
        public void Subscribe(string eventName, Action callback)
        {
            if (!eventDictionary.ContainsKey(eventName))
                eventDictionary[eventName] = null;
            eventDictionary[eventName] = (Action)eventDictionary[eventName] + callback;
        }
        public void Subscribe<T>(string eventName, Action<T> callback)
        {
            if (!eventDictionary.ContainsKey(eventName))
                eventDictionary[eventName] = null;
            eventDictionary[eventName] = (Action<T>)eventDictionary[eventName] + callback;
        }
        public void Unsubscribe<T>(string eventName, Action<T> callback)
        {
            if (eventDictionary.ContainsKey(eventName))
            {
                eventDictionary[eventName] = (Action<T>)eventDictionary[eventName] - callback;
                if (eventDictionary[eventName] == null)
                    eventDictionary.Remove(eventName);
            }
        }
        public void Invoke<T>(string eventName, T data)
        {
            if (eventDictionary.TryGetValue(eventName, out var action))
                (action as Action<T>)?.Invoke(data);
        }

        public void Invoke(string eventName)
        {
            if (eventDictionary.TryGetValue(eventName, out var action))
                (action as Action)?.Invoke();
        }

        public void Dispose()
        {
            eventDictionary.Clear();
        }
    }

}