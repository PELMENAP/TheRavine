using UnityEngine;

public delegate GameObject CreateInstance(Vector2 position, GameObject prefab);
public interface IPoolManager<T>
{
    void CreatePool(string poolKey, T prefab, CreateInstance createInstance, ushort poolSize);
    void Reuse(string prefabID, Vector2 position, bool flip, float rotateValue);
    void Deactivate(string prefabID);
    ushort GetPoolSize(string prefabID);
    void IncreasePoolSize(string prefabID);
}
