using UnityEngine;

using TheRavine.Base;
using TheRavine.EntityControl;
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
    private Vector3 baseOffset = new(0, 30, -35);
    private Vector3 zOffset = new(0, 30, -35);
    private ViewFacing facing;
    private float pitch, yaw, rotateFrom, rotateTo, rotateTime, rotateDuration;
    private bool rotating;
    private TransformComponent playerTransformComponent;
    private Vector3 targetPos, factMousePositionOffset;

    private CameraMovingConfig cameraMove;
    
    public void SetUp(PlayerEntity playerEntity, Camera camera, Transform cameraTransform, CameraMovingConfig cameraMove)
    {
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<AimAddition>(AimAdditionHandleEvent);
        playerEntity.GetEntityComponent<EventBusComponent>().EventBus.Subscribe<CameraPlace>(CameraPlaceHandleEvent);

        playerTransformComponent = playerEntity.GetEntityComponent<TransformComponent>();
        this.camera = camera;
        this.cameraTransform = cameraTransform;
        this.cameraMove = cameraMove;
        facing = ViewOrientation.Facing;
        yaw = facing.Yaw();
        pitch = cameraTransform.eulerAngles.x;
        zOffset = Quaternion.Euler(0, yaw, 0) * baseOffset;
        cameraTransform.position = playerTransformComponent.GetEntityPosition() + zOffset;
        ViewOrientation.Set(facing, pitch, yaw);
    }

    private void AimAdditionHandleEvent(AEntity entity, AimAddition e)
    {
        factMousePositionOffset = new Vector3(e.Position.x, 0, e.Position.y);
    }
    private void CameraPlaceHandleEvent(AEntity entity, CameraPlace e)
    {
        pitch = 40 - cameraMove.xRotationCurve.Evaluate(e.scale) * 20;
        baseOffset = new Vector3(0, 30 - cameraMove.yCurve.Evaluate(e.scale) * 20, -35 + cameraMove.zCurve.Evaluate(e.scale) * 15);

        if (e.facing != facing) BeginRotation(e.facing);
        else if (!rotating) ApplyView();
    }

    private void BeginRotation(ViewFacing newFacing)
    {
        facing = newFacing;
        float target = yaw + Mathf.DeltaAngle(yaw, newFacing.Yaw());
        float seconds = IsSmoothRotation() ? SimulationRules.Active.CameraRotateSeconds : 0f;

        if (seconds <= 0f)
        {
            rotating = false;
            yaw = newFacing.Yaw();
        }
        else
        {
            rotating = true;
            rotateFrom = yaw;
            rotateTo = target;
            rotateTime = 0f;
            rotateDuration = seconds;
        }

        ApplyView();
    }

    private static bool IsSmoothRotation() =>
        ServiceLocator.Services.TryGet(out GlobalSettingsController settings) && settings.GetCurrent().smoothCameraRotation;

    private void UpdateRotation()
    {
        if (!rotating) return;

        rotateTime += Time.deltaTime;
        float t = Mathf.Clamp01(rotateTime / rotateDuration);
        if (t >= 1f)
        {
            rotating = false;
            yaw = Mathf.Repeat(rotateTo, 360f);
        }
        else
        {
            yaw = Mathf.Lerp(rotateFrom, rotateTo, t * t * (3f - 2f * t));
        }

        ApplyView();
    }

    private void ApplyView()
    {
        zOffset = Quaternion.Euler(0, yaw, 0) * baseOffset;
        cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0);
        ViewOrientation.Set(facing, pitch, yaw);
    }

    public void CameraUpdate()
    {
        UpdateRotation();
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

[System.Serializable]
public struct CameraMovingConfig
{
    public AnimationCurve zCurve;
    public AnimationCurve yCurve;
    public AnimationCurve xRotationCurve;

    public CameraMovingConfig(AnimationCurve zCurve, AnimationCurve yCurve, AnimationCurve xRotationCurve)
    {
        this.zCurve = zCurve;
        this.yCurve = yCurve;
        this.xRotationCurve = xRotationCurve;
    }
}