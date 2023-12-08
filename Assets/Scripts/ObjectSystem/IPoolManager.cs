using UnityEngine;

public interface IPoolManager<T>
{
    void CreatePool(T prefab, CreateInstance createInstance, int poolSize);
    void Reuse(int prefabID, Vector2 position, float rotateValue);
    void Deactivate(int prefabID);
}
