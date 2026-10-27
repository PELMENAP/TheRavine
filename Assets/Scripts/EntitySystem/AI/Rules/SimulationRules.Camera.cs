using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField] private float cameraScaleStep = 0.05f;
    [SerializeField] private float cameraScaleMin = 0f;
    [SerializeField] private float cameraScaleMax = 1f;
    [SerializeField] private float cameraFlipDebounce = 0.25f;

    public float CameraScaleStep => cameraScaleStep;
    public float CameraScaleMin => cameraScaleMin;
    public float CameraScaleMax => cameraScaleMax;
    public float CameraFlipDebounce => cameraFlipDebounce;
}
