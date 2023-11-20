using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPoolManager<T>
{
    // ObjectSystem objectSystem { get; set; }
    void CreatePool(T prefab, int poolSize);
    void Reuse(int prefabID, Vector2 position);
}
