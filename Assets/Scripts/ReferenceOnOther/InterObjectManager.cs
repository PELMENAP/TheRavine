using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterObjectManager
{
    private static InterObjectManager _instance;
    public static InterObjectManager instance
    {
        get
        {
            if (_instance == null)
                _instance = new InterObjectManager();
            return _instance;
        }
    }
    
    Dictionary<Vector2, Triple<string, int, GameObject>> interObjectDictionary = new Dictionary<Vector2, Triple<string, int, GameObject>>(1000);
    // position: id, amount, plob

    public bool SetObjectByPosition(Vector2 position, string id, int amount, GameObject prefab)
    {
        GameObject gobject;
        if(interObjectDictionary.ContainsKey(position))
        {
            Triple<string, int, GameObject> triple = interObjectDictionary[position];
            if(id == triple.First){
                if(triple.Second == 0){
                    triple.Third = GameObject.Instantiate(prefab, position, Quaternion.identity);
                }
                triple.Second += amount;
                if(triple.Second == 0)
                    Object.Destroy(triple.Third);
                return true;
            }
            if(triple.Second == 0){
                gobject = GameObject.Instantiate(prefab, position, Quaternion.identity);
                triple = new Triple<string, int, GameObject>(id, amount, gobject);
                return true;
            }
            return false;
        }
        gobject = GameObject.Instantiate(prefab, position, Quaternion.identity);
        interObjectDictionary[position] = new Triple<string, int, GameObject>(id, amount, gobject);
        return true;
    }

    public Triple<string, int, GameObject> GetObjectByPosition(Vector2 position){
        if(!interObjectDictionary.ContainsKey(position) || interObjectDictionary[position].Second == 0)
            return null;
        return interObjectDictionary[position];
    }
}

public class Triple<T, U, V> {
    public Triple() {
    }

    public Triple(T first, U second, V third) {
        this.First = first;
        this.Second = second;
        this.Third = third;
    }

    public T First { get; set; }
    public U Second { get; set; }
    public V Third { get; set; }
};