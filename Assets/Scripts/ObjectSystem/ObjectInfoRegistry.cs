using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.ObjectControl
{

    [CreateAssetMenu(fileName = "ObjectInfoRegistry", menuName = "ScriptableObjects/ObjectInfoRegistry", order = 1)]
    public class ObjectInfoRegistry : ScriptableObject
    {
        [SerializeField]
        public List<ObjectInfo> objectInfos = new List<ObjectInfo>();

        private Dictionary<int, ObjectInfo> _data;
        private Dictionary<int, ObjectInfo> Data
        {
            get
            {
                if (_data == null)
                {
                    RebuildDictionary();
                }
                return _data;
            }
        }

        public ObjectInfo Get(int prefabID)
        {
            Data.TryGetValue(prefabID, out var value);
            return value;
        }

        public void Clear()
        {
            if (_data != null)
                _data.Clear();
        }
        void OnDisable()
        {
            Clear();
        }

        private void RebuildDictionary()
        {
            if (_data == null)
                _data = new Dictionary<int, ObjectInfo>(objectInfos.Count);
            else
                _data.Clear();

            foreach (var info in objectInfos)
            {
                if (info != null && !_data.ContainsKey(info.PrefabID))
                {
                    // Debug.Log(info.name + " " + info.PrefabID);
                    _data[info.PrefabID] = info;
                }
            }
        }
    }
}