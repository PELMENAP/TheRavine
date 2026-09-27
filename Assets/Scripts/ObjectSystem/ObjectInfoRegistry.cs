using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheRavine.ObjectControl
{

    [CreateAssetMenu(fileName = "ObjectInfoRegistry", menuName = "ScriptableObjects/ObjectInfoRegistry", order = 1)]
    public class ObjectInfoRegistry : ScriptableObject
    {
        public const int NoneId = 0;
        public const int FirstReservedId = 0xF000;

        [SerializeField]
        public List<ObjectInfo> objectInfos = new();

        private ObjectInfo[] byId = Array.Empty<ObjectInfo>();
        private int[] denseById = Array.Empty<int>();
        private ObjectInfo[] dense = Array.Empty<ObjectInfo>();
        private int denseCount;
        private readonly Dictionary<GameObject, int> idByPrefab = new();

        public int Count => denseCount;

        public ObjectInfo Get(int id) => (uint)id < (uint)byId.Length ? byId[id] : null;

        public int DenseIndexOf(int id) => (uint)id < (uint)denseById.Length ? denseById[id] : -1;

        public ObjectInfo GetDense(int denseIndex) => dense[denseIndex];

        public bool TryGetIdByPrefab(GameObject prefab, out int id)
        {
            id = NoneId;
            return prefab != null && idByPrefab.TryGetValue(prefab, out id);
        }

        public void Clear()
        {
            byId = Array.Empty<ObjectInfo>();
            denseById = Array.Empty<int>();
            dense = Array.Empty<ObjectInfo>();
            denseCount = 0;
            idByPrefab.Clear();
        }

        public void Rebuild()
        {
            Clear();

            int maxId = 0;
            for (int i = 0; i < objectInfos.Count; i++)
            {
                ObjectInfo info = objectInfos[i];
                if (info != null && info.Id > maxId && info.Id < FirstReservedId) maxId = info.Id;
            }

            byId = new ObjectInfo[maxId + 1];
            denseById = new int[maxId + 1];
            dense = new ObjectInfo[objectInfos.Count];
            Array.Fill(denseById, -1);

            for (int i = 0; i < objectInfos.Count; i++)
            {
                ObjectInfo info = objectInfos[i];
                if (info == null) continue;

                int id = info.Id;
                if (id == NoneId || id >= FirstReservedId)
                {
                    Debug.LogError($"[ObjectInfoRegistry] {info.name}: invalid id {id}");
                    continue;
                }
                if (byId[id] != null)
                {
                    Debug.LogError($"[ObjectInfoRegistry] {info.name}: duplicate id {id} ({byId[id].name})");
                    continue;
                }

                byId[id] = info;
                denseById[id] = denseCount;
                dense[denseCount++] = info;

                if (info.ObjectPrefab != null)
                    idByPrefab.TryAdd(info.ObjectPrefab, id);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var used = new HashSet<int>();
            for (int i = 0; i < objectInfos.Count; i++)
            {
                ObjectInfo info = objectInfos[i];
                if (info == null) continue;
                if (info.Id == NoneId || info.Id >= FirstReservedId || !used.Add(info.Id))
                    Debug.LogWarning($"[ObjectInfoRegistry] {info.name}: id {info.Id} is missing, reserved or duplicated. Use 'Assign Missing Ids'.", this);
            }
        }

        [ContextMenu("Assign Missing Ids")]
        private void AssignMissingIds()
        {
            var used = new HashSet<int>();
            int next = 1;

            for (int i = 0; i < objectInfos.Count; i++)
            {
                ObjectInfo info = objectInfos[i];
                if (info == null || info.Id == NoneId || info.Id >= FirstReservedId) continue;
                if (!used.Add(info.Id)) continue;
                if (info.Id >= next) next = info.Id + 1;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < objectInfos.Count; i++)
            {
                ObjectInfo info = objectInfos[i];
                if (info == null) continue;

                bool valid = info.Id != NoneId && info.Id < FirstReservedId && seen.Add(info.Id);
                if (valid) continue;

                while (used.Contains(next)) next++;
                if (next >= FirstReservedId)
                {
                    Debug.LogError("[ObjectInfoRegistry] id space exhausted", this);
                    return;
                }

                UnityEditor.Undo.RecordObject(info, "Assign Object Id");
                info.EditorAssignId((ushort)next);
                UnityEditor.EditorUtility.SetDirty(info);
                used.Add(next);
                seen.Add(next);
                next++;
            }

            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif
    }
}
