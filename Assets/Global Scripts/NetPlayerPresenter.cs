using UnityEngine;
using Unity.Netcode;

using TheRavine.EntityControl;
using TheRavine.Services;

public class NetPlayerPresenter : NetworkBehaviour {
    [SerializeField] private PlayerEntity playerEntity;
    [SerializeField] private CM cm;
    // public override void OnNetworkSpawn() {
    //     playerEntity.SetCamera(cm);
    //     cm.SetPlayerEntity(playerEntity);

    //     ServiceLocator locator = ServiceLocatorAccess.inst.serviceLocator;
    //     locator.GetService<EntitySystem>().AddToGlobal(playerEntity);
    //     playerEntity.SetUp(null, locator);
    //     Debug.Log("spawn player");
    // }
}