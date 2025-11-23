using UnityEngine;
using Unity.Netcode;

using TheRavine.EntityControl;
using TheRavine.Events;
public class CM : NetworkBehaviour
{
    private PlayerEntity playerEntity;
    public void SetPlayerEntity(PlayerEntity player)
    {
        playerEntity = player;
    }
    private Transform cameraTransform;
    [SerializeField] private float Velocity, MinDistance;
    [SerializeField] private Vector3 zOffset = new(0, 0, -10);
    private TransformComponent playerTransformComponent;
    private Vector3 targetPos, factMousePositionOffset;
    public void SetUp(ISetAble.Callback callback)
    {
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<AimAddition>(AimAdditionHandleEvent);
        playerTransformComponent = playerEntity.GetEntityComponent<TransformComponent>();
        cameraTransform = this.transform;
        cameraTransform.position = (Vector3)playerTransformComponent.GetEntityPosition() + zOffset;
        callback?.Invoke();
    }

    private void AimAdditionHandleEvent(AEntity entity, AimAddition e)
    {
        factMousePositionOffset = e.Position;
    }

    public void CameraUpdate()
    {
        UpdateDefault();
    }
    private void UpdateDefault()
    {
        targetPos = (Vector3)playerTransformComponent.GetEntityPosition() + factMousePositionOffset + zOffset;
        if (Vector3.Distance(cameraTransform.localPosition, targetPos) < MinDistance) return;
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetPos, Velocity * Time.deltaTime);
    }

    public void BreakUp(ISetAble.Callback callback)
    {
        callback?.Invoke();
    }
}