using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StaticObjectManager
{
    // Dictionary<Vector2, Triple<string, int, ObjectInstance>> interObjectDictionary = new Dictionary<Vector2, Triple<string, int, ObjectInstance>>(1000);
    // position: id, amount, prefabID

    // public bool SetObjectByPosition(Vector2 position, string id, int amount, ObjectInstance prefab)
    // {
    //     if (interObjectDictionary.ContainsKey(position))
    //     {
    //         Triple<string, int, ObjectInstance> triple = interObjectDictionary[position];
    //         if (id == triple.First)
    //         {
    //             if (triple.Second == 0)
    //             {
    //                 parent.DeleteObjectFromPosition(prefab.prefabID, position);
    //             }
    //             triple.Second += amount;
    //             if (triple.Second == 0)
    //                 triple.Third.gameObject.SetActive(false);
    //             return true;
    //         }
    //         if (triple.Second == 0)
    //         {
    //             parent.ReuseObjectToPosition(prefab.prefabID, position);
    //             triple = new Triple<string, int, ObjectInstance>(id, amount, prefab);
    //             return true;
    //         }
    //         return false;
    //     }
    //     parent.ReuseObjectToPosition(prefab.prefabID, position);
    //     interObjectDictionary[position] = new Triple<string, int, ObjectInstance>(id, amount, prefab);
    //     return true;
    // }

    // public Triple<string, int, ObjectInstance> GetObjectByPosition(Vector2 position)
    // {
    //     if (!interObjectDictionary.ContainsKey(position))
    //         return null;
    //     return interObjectDictionary[position];
    // }
}
