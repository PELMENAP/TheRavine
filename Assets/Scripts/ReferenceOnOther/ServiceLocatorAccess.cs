using UnityEngine;
using Unity.Netcode;

using TheRavine.Services;
using TheRavine.Netcode;
using TheRavine.Base;
public class ServiceLocatorAccess : MonoBehaviour
{
    public static ServiceLocatorAccess inst;
    public ServiceLocator serviceLocator;
    [SerializeField] private NetworkSpawner networkSpawner;
    [SerializeField] private DayCycle dayCycle;

    public void NetSpawnObject(string prefabName) => networkSpawner.SpawnObjectServerRpc(prefabName);
    public NetworkObject GetCurrentSpawnedObject() => networkSpawner.GetCurrentSpawnedObject();

    private void Awake() 
    {
        inst = this;
        // networkSpawner.Spawn();
    }
    
}
