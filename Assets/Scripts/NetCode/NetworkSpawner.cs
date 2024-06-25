using UnityEngine;
using Unity.Netcode;

namespace TheRavine.Netcode
{
    public class NetworkSpawner : NetworkBehaviour
    {
        private NetworkObject currentObject;
        public NetworkObject GetCurrentSpawnedObject() => currentObject;
        
        [ServerRpc(RequireOwnership = false)]
        public void SpawnObjectServerRpc(string prefabName, ServerRpcParams rpcParams = default)
        {
            GameObject prefab = GetPrefabByName(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning("Invalid prefab name!");
                return;
            }

            NetworkObject networkObject = Instantiate(prefab).GetComponent<NetworkObject>();
            networkObject.Spawn();

            ReturnSpawnedObjectClientRpc(networkObject.NetworkObjectId, rpcParams.Receive.SenderClientId);
        }
        [ClientRpc]
        private void ReturnSpawnedObjectClientRpc(ulong networkObjectId, ulong clientId, ClientRpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                NetworkObject spawnedObject = NetworkManager.Singleton.SpawnManager.SpawnedObjects[networkObjectId];
                currentObject = spawnedObject;
                OnObjectSpawned(spawnedObject);
            }
        }
        private void OnObjectSpawned(NetworkObject spawnedObject)
        {
            Debug.Log($"Object spawned with ID: {spawnedObject.NetworkObjectId}");
        }
        private GameObject GetPrefabByName(string prefabName)
        {
            int length = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs.Count;
            for(int i = 0; i < length; i++)
            {
                if (NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs[i].Prefab.name == prefabName)
                {
                    return NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs[i].Prefab;
                }
            }
            return null;
        }
    }
}