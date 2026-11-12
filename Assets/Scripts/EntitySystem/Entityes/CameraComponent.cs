using UnityEngine;

using TheRavine.Base;
using TheRavine.EntityControl;
using TheRavine.Generator;
using TheRavine.ViewControl;

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
    private float yaw, pitch, distance;
    private TransformComponent playerTransformComponent;
    private Vector3 pivot, targetPos, factMousePositionOffset;
    private MapGenerator map;

    public void SetUp(PlayerEntity playerEntity, Camera camera, Transform cameraTransform)
    {
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<AimAddition>(AimAdditionHandleEvent);
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<CameraPlace>(CameraPlaceHandleEvent);

        playerTransformComponent = playerEntity.GetEntityComponent<TransformComponent>();
        this.camera = camera;
        this.cameraTransform = cameraTransform;

        var rules = SimulationRules.Active;
        yaw = ViewOrientation.YawDegrees;
        pitch = rules.CameraPitchInitial;
        distance = DistanceFor(0f);

        pivot = playerTransformComponent.GetEntityPosition();
        ApplyView();
    }

    private void AimAdditionHandleEvent(AEntity entity, AimAddition e)
    {
        factMousePositionOffset = new Vector3(e.Position.x, 0, e.Position.y);
    }

    private void CameraPlaceHandleEvent(AEntity entity, CameraPlace e)
    {
        var rules = SimulationRules.Active;
        yaw = e.yaw;
        pitch = Mathf.Clamp(e.pitch, rules.CameraPitchMin, rules.CameraPitchMax);
        distance = DistanceFor(e.scale);
        ApplyView();
    }

    private static float DistanceFor(float scale)
    {
        var rules = SimulationRules.Active;
        return Mathf.Lerp(rules.CameraDistanceMax, rules.CameraDistanceMin, scale);
    }

    private void ApplyView()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = pivot + rotation * new Vector3(0f, 0f, -distance);

        if (map != null || ServiceLocator.Services.TryGet(out map))
        {
            if (map.TrySampleHeightBilinear(position.x, position.z, out float ground))
                position.y = Mathf.Max(position.y, ground + SimulationRules.Active.CameraTerrainClearance);
        }

        cameraTransform.SetPositionAndRotation(position, rotation);
        ViewOrientation.SetPivot(pivot);
        ViewOrientation.Set(yaw, pitch);
    }

    public void CameraUpdate()
    {
        UpdatePivot();
        ApplyView();
    }

    private void UpdatePivot()
    {
        targetPos = playerTransformComponent.GetEntityPosition() + factMousePositionOffset;

        var delta = targetPos - pivot;
        var dist = delta.magnitude;

        if (dist < MinDistance)
            return;

        if (dist < SoftDistance)
        {
            var t = Mathf.InverseLerp(MinDistance, SoftDistance, dist);
            var lerpSpeed = SoftLerpSpeed * t;

            pivot = Vector3.Lerp(pivot, targetPos, lerpSpeed * Time.deltaTime);
            return;
        }

        var speed = dist * FollowFactor;

        pivot = Vector3.MoveTowards(pivot, targetPos, speed * Time.deltaTime);
    }

    public void Dispose()
    {
        ViewOrientation.ClearPivot();
    }

    public Camera GetCamera() => camera;
}
