using UnityEngine;

public sealed partial class SimulationRules
{
    [SerializeField] private float cameraScaleStep = 0.05f;
    [SerializeField] private float cameraScaleMin = 0f;
    [SerializeField] private float cameraScaleMax = 1f;

    [Header("Camera: free look")]
    [Tooltip("Поворот вокруг вертикали за drag на всю сторону квадрата управления, градусов.")]
    [SerializeField, Min(0f)] private float cameraYawDegreesPerSide = 180f;
    [Tooltip("Наклон за drag на всю сторону квадрата управления, градусов.")]
    [SerializeField, Min(0f)] private float cameraPitchDegreesPerSide = 60f;
    [SerializeField, Range(-89f, 89f)] private float cameraPitchMin = 15f;
    [SerializeField, Range(0f, 89f)] private float cameraPitchMax = 65f;
    [SerializeField, Range(0f, 89f)] private float cameraPitchInitial = 40f;
    [Tooltip("Смещение указателя в пикселях, после которого нажатие становится drag и подавляет клик.")]
    [SerializeField, Min(0f)] private float cameraDragThresholdPixels = 6f;

    [Tooltip("Инвертировать горизонталь drag: по умолчанию вправо — камера поворачивается влево.")]
    [SerializeField] private bool cameraInvertDragYaw = false;
    [Tooltip("Инвертировать вертикаль drag: по умолчанию вверх — камера наклоняется вниз.")]
    [SerializeField] private bool cameraInvertDragPitch = false;

    [Header("Camera: anchor")]
    [Tooltip("Высота точки, на которую смотрит камера, над позицией игрока, мировых единиц. 0 — в ноги.")]
    [SerializeField] private float cameraPivotHeight = 1.5f;

    [Header("Camera: distance")]
    [Tooltip("Расстояние от точки опоры до камеры: scale 0 — максимум, scale 1 — минимум.")]
    [SerializeField, Min(1f)] private float cameraDistanceMax = 46f;
    [SerializeField, Min(1f)] private float cameraDistanceMin = 14f;
    [Tooltip("Запас камеры над рельефом под ней, мировых единиц.")]
    [SerializeField, Min(0f)] private float cameraTerrainClearance = 2f;

    public float CameraScaleStep => cameraScaleStep;
    public float CameraScaleMin => cameraScaleMin;
    public float CameraScaleMax => cameraScaleMax;
    public float CameraYawDegreesPerSide => cameraYawDegreesPerSide;
    public float CameraPitchDegreesPerSide => cameraPitchDegreesPerSide;
    public float CameraPitchMin => Mathf.Min(cameraPitchMin, cameraPitchMax);
    public float CameraPitchMax => Mathf.Max(cameraPitchMin, cameraPitchMax);
    public float CameraPitchInitial => Mathf.Clamp(cameraPitchInitial, CameraPitchMin, CameraPitchMax);
    public float CameraDragThresholdPixels => cameraDragThresholdPixels;
    public bool CameraInvertDragYaw => cameraInvertDragYaw;
    public bool CameraInvertDragPitch => cameraInvertDragPitch;
    public float CameraPivotHeight => cameraPivotHeight;
    public float CameraDistanceMax => Mathf.Max(cameraDistanceMin, cameraDistanceMax);
    public float CameraDistanceMin => Mathf.Min(cameraDistanceMin, cameraDistanceMax);
    public float CameraTerrainClearance => cameraTerrainClearance;
}
