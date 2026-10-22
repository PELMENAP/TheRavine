using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace TheRavine.Netcode
{
    public class NetworkSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject[] spawnablePrefabs;
        [SerializeField, Min(0f)] private float maxSpawnRadius = 10f;
        [SerializeField, Min(1)] private int maxSpawnsPerWindow = 4;
        [SerializeField, Min(0.01f)] private float rateWindowSeconds = 1f;

        private struct RateState
        {
            public double WindowStart;
            public int    Count;
            public bool   Reported;
        }

        private NetworkObject[] _whitelist = System.Array.Empty<NetworkObject>();
        private readonly HashSet<ulong> _spawnRights = new();
        private readonly Dictionary<ulong, RateState> _rates = new();
        private readonly ulong[] _replyTarget = new ulong[1];
        private NetworkObject currentObject;

        public NetworkObject GetCurrentSpawnedObject() => currentObject;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            BuildWhitelist();
            NetworkManager.OnClientDisconnectCallback += RevokeSpawnRights;
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null) NetworkManager.OnClientDisconnectCallback -= RevokeSpawnRights;
            _spawnRights.Clear();
            _rates.Clear();
        }

        public void GrantSpawnRights(ulong clientId)
        {
            if (IsServer) _spawnRights.Add(clientId);
        }

        public void RevokeSpawnRights(ulong clientId)
        {
            _spawnRights.Remove(clientId);
            _rates.Remove(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpawnObjectServerRpc(int prefabId, Vector3 position, ServerRpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;

            if (!IsAuthorizedClient(sender, out var client))
            {
                Reject(sender, "нет прав на спавн");
                return;
            }

            if (!ConsumeRate(sender)) return;

            var prefab = GetWhitelistedPrefab(prefabId);
            if (prefab == null)
            {
                Reject(sender, $"prefabId {prefabId} вне whitelist");
                return;
            }

            if (!IsWithinRadius(client, position))
            {
                Reject(sender, $"позиция {position} дальше {maxSpawnRadius} от игрока");
                return;
            }

            var networkObject = Instantiate(prefab, position, Quaternion.identity);
            networkObject.Spawn();

            _replyTarget[0] = sender;
            ReturnSpawnedObjectClientRpc(networkObject.NetworkObjectId, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = _replyTarget }
            });
        }

        [ClientRpc]
        private void ReturnSpawnedObjectClientRpc(ulong networkObjectId, ClientRpcParams rpcParams = default)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject spawnedObject))
            {
                currentObject = spawnedObject;
                OnObjectSpawned(spawnedObject);
            }
            else
            {
                RavineLog.Error($"NetworkSpawner: заспавненный объект {networkObjectId} не найден");
            }
        }

        private void OnObjectSpawned(NetworkObject spawnedObject)
        {
            RavineLog.Info($"NetworkSpawner: объект заспавнен, ID {spawnedObject.NetworkObjectId}");
        }

        private void BuildWhitelist()
        {
            int n = spawnablePrefabs != null ? spawnablePrefabs.Length : 0;
            _whitelist = new NetworkObject[n];
            var registered = NetworkManager.NetworkConfig.Prefabs;

            for (int i = 0; i < n; i++)
            {
                var prefab = spawnablePrefabs[i];
                if (prefab == null) continue;
                if (!registered.Contains(prefab.gameObject))
                {
                    RavineLog.Error($"NetworkSpawner: prefabId {i} ({prefab.name}) не зарегистрирован в NetworkConfig.Prefabs, исключён из whitelist");
                    continue;
                }
                _whitelist[i] = prefab;
            }
        }

        private NetworkObject GetWhitelistedPrefab(int prefabId)
            => (uint)prefabId < (uint)_whitelist.Length ? _whitelist[prefabId] : null;

        private bool IsAuthorizedClient(ulong clientId, out NetworkClient client)
        {
            client = null;
            if (!IsServer || !NetworkManager.ConnectedClients.TryGetValue(clientId, out client)) return false;
            if (client.PlayerObject == null) return false;
            return clientId == OwnerClientId || _spawnRights.Contains(clientId);
        }

        private bool IsWithinRadius(NetworkClient client, Vector3 position)
            => (position - client.PlayerObject.transform.position).sqrMagnitude <= maxSpawnRadius * maxSpawnRadius;

        private bool ConsumeRate(ulong clientId)
        {
            double now = NetworkManager.ServerTime.Time;
            _rates.TryGetValue(clientId, out var state);

            if (now - state.WindowStart >= rateWindowSeconds)
            {
                state.WindowStart = now;
                state.Count       = 0;
                state.Reported    = false;
            }

            bool allowed = state.Count < maxSpawnsPerWindow;
            if (allowed) state.Count++;
            else if (!state.Reported)
            {
                state.Reported = true;
                Reject(clientId, $"превышен лимит {maxSpawnsPerWindow} спавнов за {rateWindowSeconds} с");
            }

            _rates[clientId] = state;
            return allowed;
        }

        private static void Reject(ulong clientId, string reason)
            => RavineLog.Warning($"NetworkSpawner: отказ клиенту {clientId}: {reason}");
    }
}
