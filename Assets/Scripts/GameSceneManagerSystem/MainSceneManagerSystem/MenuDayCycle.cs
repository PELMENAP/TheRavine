using R3;
using UnityEngine;

namespace TheRavine.Base
{
    [RequireComponent(typeof(Light))]
    public class MenuDayCycle : MonoBehaviour
    {
        public ReadOnlyReactiveProperty<bool> IsDay => isDay;
        public ReadOnlyReactiveProperty<float> NormalizedTime => normalizedTime;

        [Header("Cycle Settings")]
        [Tooltip("Длительность полного цикла (суток) в реальных секундах")]
        [SerializeField] private float dayDurationSeconds = 120f;
        [Range(0f, 1f)]
        [SerializeField] private float initialTime = 0.6f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Lighting & Skybox")]
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private AnimationCurve intensityCurve;
        [SerializeField] private AnimationCurve nightBlendCurve;
        [SerializeField] private Transform secondSun;
        [SerializeField] private Material skyboxMaterial;

        private readonly static int nightBlendId = Shader.PropertyToID("_NightBlend");
        private const float RotationSpeedX = 360f;

        private readonly ReactiveProperty<bool> isDay = new(false);
        private readonly ReactiveProperty<float> normalizedTime = new(0f);

        private Light sun;
        private float currentTime;
        private Material runtimeSkybox;

        private void Awake()
        {
            sun = GetComponent<Light>();
            currentTime = initialTime;

            if (skyboxMaterial == null && RenderSettings.skybox != null)
                skyboxMaterial = RenderSettings.skybox;

            if (skyboxMaterial != null)
            {
                runtimeSkybox = new Material(skyboxMaterial);
                RenderSettings.skybox = runtimeSkybox;
            }

            ApplyVisuals(currentTime);
        }

        private void Update()
        {
            if (dayDurationSeconds <= 0f) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            currentTime = Mathf.Repeat(currentTime + (dt / dayDurationSeconds), 1f);

            normalizedTime.Value = currentTime;

            bool nowDay = currentTime >= DayConstants.DayStart && currentTime <= DayConstants.DayEnd;
            if (isDay.Value != nowDay)
                isDay.Value = nowDay;

            ApplyVisuals(currentTime);
        }

        private void ApplyVisuals(float t)
        {
            float xAngle = t * RotationSpeedX;
            transform.localRotation = Quaternion.Euler(xAngle, 0f, 0f);

            if (secondSun != null)
                secondSun.localRotation = Quaternion.Euler(xAngle - 180f, 0f, 0f);

            if (sun != null)
            {
                sun.color = sunGradient.Evaluate(t);
                sun.intensity = intensityCurve.Evaluate(t);
            }

            if (runtimeSkybox != null)
            {
                runtimeSkybox.SetFloat(nightBlendId, nightBlendCurve.Evaluate(t));
            }
        }

        private void OnDestroy()
        {
            isDay?.Dispose();
            normalizedTime?.Dispose();

            if (runtimeSkybox != null)
                Destroy(runtimeSkybox);
        }
    }
}