using System.Collections.Generic;


namespace TheRavine.ObjectControl
{

    public class ObjectInfoRegistry
    {
        private readonly Dictionary<int, ObjectInfo> data = new(64);

        public void Register(ObjectInfo info)
        {
            try
            {
                data[info.PrefabID] = info;
            }
            catch
            {
                UnityEngine.Debug.Log(info.ObjectName);
                
            }
        }

        public ObjectInfo Get(int prefabID)
        {
            return data.TryGetValue(prefabID, out var value)
                ? value
                : null;
        }

        public void Clear() => data.Clear();
    }
}