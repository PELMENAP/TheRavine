using UnityEngine;

public interface IPoolManager<T>
{
    void CreatePool(T prefab, CreateInstance createInstance, ushort poolSize);
    void Reuse(int prefabID, Vector2 position, bool flip, float rotateValue);
    void Deactivate(int prefabID);
    ushort GetPoolSize(int prefabID);
    void IncreasePoolSize(int prefabID);
}
