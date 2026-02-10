using UnityEngine;

using TheRavine.EntityControl;

public interface ICameraComponent : IComponent
{
    void CameraUpdate();
    Camera GetCamera();
}

public class CameraComponent : ICameraComponent
{
    private Camera camera;
    private Transform cameraTransform;
    private readonly float MinDistance = 2f, SoftDistance = 4f;

    private readonly float SoftLerpSpeed = 2f;

    private readonly float FollowFactor = 4f;
    private Vector3 zOffset = new(0, 30, -35);
    private TransformComponent playerTransformComponent;
    private Vector3 targetPos, factMousePositionOffset;
    
    public void SetUp(PlayerEntity playerEntity, Camera camera, Transform cameraTransform)
    {
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<AimAddition>(AimAdditionHandleEvent);
        playerTransformComponent = playerEntity.GetEntityComponent<TransformComponent>();
        this.camera = camera;
        this.cameraTransform = cameraTransform;
        cameraTransform.position = playerTransformComponent.GetEntityPosition() + zOffset;
    }

    private void AimAdditionHandleEvent(AEntity entity, AimAddition e)
    {
        factMousePositionOffset = new Vector3(e.Position.x, 0, e.Position.y);
    }

    public void CameraUpdate()
    {
        UpdateDefault();
    }

    private void UpdateDefault()
    {
        targetPos = playerTransformComponent.GetEntityPosition()
                    + factMousePositionOffset
                    + zOffset;

        var currentPos = cameraTransform.position;
        var delta = targetPos - currentPos;
        var distance = delta.magnitude;

        if (distance < MinDistance)
            return;

        if (distance < SoftDistance)
        {
            var t = Mathf.InverseLerp(MinDistance, SoftDistance, distance);
            var lerpSpeed = SoftLerpSpeed * t;

            cameraTransform.position = Vector3.Lerp(
                currentPos,
                targetPos,
                lerpSpeed * Time.deltaTime
            );

            return;
        }

        var speed = distance * FollowFactor;

        cameraTransform.position = Vector3.MoveTowards(
            currentPos,
            targetPos,
            speed * Time.deltaTime
        );
    }
    public void Dispose()
    {
    }

    public Camera GetCamera() => camera;
}