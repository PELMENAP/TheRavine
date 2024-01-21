namespace TheRavine.Events
{
    public class EventBusByName
    {
        private System.Collections.Generic.Dictionary<string, System.Delegate> eventDictionary = new System.Collections.Generic.Dictionary<string, System.Delegate>();
        public void Subscribe<T>(string eventName, System.Action<T> callback)
        {
            if (!eventDictionary.ContainsKey(eventName))
                eventDictionary[eventName] = null;
            eventDictionary[eventName] = (System.Action<T>)eventDictionary[eventName] + callback;
        }
        public void Unsubscribe<T>(string eventName, System.Action<T> callback)
        {
            if (eventDictionary.ContainsKey(eventName))
            {
                eventDictionary[eventName] = (System.Action<T>)eventDictionary[eventName] - callback;
                if (eventDictionary[eventName] == null)
                    eventDictionary.Remove(eventName);
            }
        }
        public void Invoke<T>(string eventName, T data)
        {
            if (eventDictionary.TryGetValue(eventName, out var action))
                (action as System.Action<T>)?.Invoke(data);
        }

        public void Dispose()
        {
            eventDictionary.Clear();
        }
    }

}