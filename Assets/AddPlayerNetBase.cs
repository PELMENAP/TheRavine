using UnityEngine;
using Unity.Netcode;

using TheRavine.EntityControl;
using TheRavine.Services;

public class AddPlayerNetBase : NetworkBehaviour {
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private CM cm;
    // public override void OnNetworkSpawn() {
    //     NetworkObject playerObject = NetworkManager.SpawnManager.InstantiateAndSpawn(playerPrefab, OwnerClientId);
    //     playerObject.TrySetParent(this.transform);

    //     PlayerEntity playerEntity = playerPrefab.GetComponent<PlayerEntity>();

    //     playerEntity.SetCamera(cm);
    //     cm.SetPlayerEntity(playerEntity);

    //     ServiceLocator locator = ServiceLocatorAccess.inst.serviceLocator;
    //     locator.GetService<EntitySystem>().AddToGlobal(playerEntity);
    //     playerEntity.SetUp(null, locator);
    //     Debug.Log("spawn player");
    // }
}