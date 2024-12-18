using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

namespace TheRavine.Netcode
{
    public class NetworkSpawner : NetworkBehaviour
    {
        private NetworkObject currentObject;
        private Dictionary<string, GameObject> prefabDictionary;

        public NetworkObject GetCurrentSpawnedObject() => currentObject;

        private void Awake()
        {
            prefabDictionary = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
                .ToDictionary(prefab => prefab.Prefab.name, prefab => prefab.Prefab);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnObjectServerRpc(string prefabName, ServerRpcParams rpcParams = default)
        {
            if (!IsAuthorizedClient(rpcParams.Receive.SenderClientId))
            {
                Debug.LogWarning($"Unauthorized client {rpcParams.Receive.SenderClientId} attempted to spawn an object!");
                return;
            }

            GameObject prefab = GetPrefabByName(prefabName);
            if (prefab == null)
            {
                Debug.LogWarning("Invalid prefab name!");
                return;
            }

            NetworkObject networkObject = Instantiate(prefab).GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("Prefab does not contain a NetworkObject component!");
                return;
            }

            networkObject.Spawn();
            ReturnSpawnedObjectClientRpc(networkObject.NetworkObjectId, rpcParams.Receive.SenderClientId);
        }

        [ClientRpc]
        private void ReturnSpawnedObjectClientRpc(ulong networkObjectId, ulong clientId, ClientRpcParams rpcParams = default)
        {
            if (NetworkManager.Singleton.LocalClientId == clientId)
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject spawnedObject))
                {
                    currentObject = spawnedObject;
                    OnObjectSpawned(spawnedObject);
                }
                else
                {
                    Debug.LogError("Spawned object not found!");
                }
            }
        }

        private void OnObjectSpawned(NetworkObject spawnedObject)
        {
            Debug.Log($"Object spawned with ID: {spawnedObject.NetworkObjectId}");
        }

        private GameObject GetPrefabByName(string prefabName)
        {
            prefabDictionary.TryGetValue(prefabName, out GameObject prefab);
            return prefab;
        }

        private bool IsAuthorizedClient(ulong clientId)
        {
            return clientId == NetworkManager.Singleton.LocalClientId || IsServer;
        }
    }
}