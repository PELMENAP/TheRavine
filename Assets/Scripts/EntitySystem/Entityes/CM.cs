using UnityEngine;
using Unity.Netcode;

using TheRavine.Services;
using TheRavine.EntityControl;
using TheRavine.Events;
public class CM : NetworkBehaviour
{
    private PlayerEntity playerEntity;
    public void SetPlayerEntity(PlayerEntity player)
    {
        playerEntity = player;
    }
    private Transform cameratrans;
    [SerializeField] private float Velocity, MinDistance;
    private TransformComponent playerTransformComponent;
    private Vector3 targetPos, factMousePositionOffset, zOffset = new Vector3(0, 0, -10);
    public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
    {
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<Vector3>(nameof(AimAddition), AimAdditionHandleEvent);
        playerTransformComponent = playerEntity.GetEntityComponent<TransformComponent>();
        cameratrans = this.transform;
        cameratrans.position = (Vector3)playerTransformComponent.GetEntityPosition() + zOffset;
        callback?.Invoke();
    }

    private void AimAdditionHandleEvent(Vector3 factMousePosition)
    {
        factMousePositionOffset = factMousePosition;
    }

    public void CameraUpdate()
    {
        UpdateDefault();
    }
    private void UpdateDefault()
    {
        targetPos = (Vector3)playerTransformComponent.GetEntityPosition() + factMousePositionOffset + zOffset;
        if (Vector3.Distance(cameratrans.localPosition, targetPos) < MinDistance) return;
        cameratrans.localPosition = Vector3.Lerp(cameratrans.localPosition, targetPos, Velocity * Time.deltaTime);
    }

    // private void UpdateForMap()
    // {
    //     if (Input.GetKey("["))
    //     {
    //         mainCam.orthographicSize -= 20;
    //     }
    //     else if (Input.GetKey("]"))
    //     {
    //         mainCam.orthographicSize += 20;
    //     }
    //     else if (Input.mouseScrollDelta.y != 0)
    //     {
    //         mainCam.orthographicSize += Input.mouseScrollDelta.y * 20 * mainCam.orthographicSize / 300;
    //         this.transform.Translate(new Vector3((Input.mousePosition.x - Screen.width / 2) * 0.5f, (Input.mousePosition.y - Screen.height / 2) * 0.5f, 0) * (Input.mouseScrollDelta.y > 0 ? -1 : 1) * mainCam.orthographicSize / 200);
    //     }
    //     if (mainCam.orthographicSize > 1000)
    //     {
    //         mainCam.orthographicSize = 1000;
    //     }
    //     else if (mainCam.orthographicSize < 10)
    //     {
    //         mainCam.orthographicSize = 10;
    //     }
    // }

    public void BreakUp(ISetAble.Callback callback)
    {
        callback?.Invoke();
    }
}