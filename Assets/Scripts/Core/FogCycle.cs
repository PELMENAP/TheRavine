using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using R3;
using UnityEngine;
using Unity.Mathematics;

namespace TheRavine.Base
{
    public class FogCycle : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DayCycle dayCycle;
        [SerializeField] private Material fogMaterial;

        [Header("Color & Density")]
        [SerializeField] private AnimationCurve densityCurve = AnimationCurve.Constant(0, 1, 0.05f);

        [Header("Height Mask")]
        [SerializeField] private AnimationCurve heightMaskLengthCurve = AnimationCurve.Constant(0, 1, 16f);

        [SerializeField] private bool disableAtNight = false;
        [SerializeField] private float fadeDuration = 2f;

        [Header("Appearance")]
        [SerializeField, Min(0f)] private float appearDelay = 5f;
        [SerializeField, Min(0.01f)] private float appearDuration = 40f;
        [SerializeField, Range(0f, 1f)] private float appearStartHeight = 0.2f;
        [SerializeField, Min(0.01f)] private float multiplierSmoothTime = 20f;

        private CancellationTokenSource cts;
        private CancellationTokenSource fadeCts;
        private readonly CompositeDisposable subscriptions = new();

        private float currentVisibility = 1f;
        private bool wasDay;

        private float activeDensityMultiplier = 1f;
        private float pendingDensityMultiplier = 1f;

        private float activeHeightMaskLengthMultiplier = 1f;
        private float pendingHeightMaskLengthMultiplier = 1f;

        private float shownDensityMultiplier = 1f;
        private float shownHeightMaskLengthMultiplier = 1f;
        private float densityVelocity;
        private float heightVelocity;

        private float normalizedTime;
        private float appear;
        private float appearTimer;
        private bool fogEnabled = true;

        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int HeightMaskLengthId = Shader.PropertyToID("_HeightMaskLength");


        public void OnEnable()
        {
            cts = new CancellationTokenSource();
            fadeCts = new CancellationTokenSource();

            activeDensityMultiplier = GenerateDensityMultiplier();
            pendingDensityMultiplier = GenerateDensityMultiplier();

            activeHeightMaskLengthMultiplier = GenerateDensityMultiplier();
            pendingHeightMaskLengthMultiplier = GenerateDensityMultiplier();

            shownDensityMultiplier = activeDensityMultiplier;
            shownHeightMaskLengthMultiplier = activeHeightMaskLengthMultiplier;
            densityVelocity = 0f;
            heightVelocity = 0f;
            appear = 0f;
            appearTimer = 0f;
            Apply();

            wasDay = dayCycle.IsDay.CurrentValue;

            dayCycle.NormalizedTime
                .Subscribe(OnTimeChanged)
                .AddTo(subscriptions);

            dayCycle.IsDay
                .Subscribe(OnDayNightChanged)
                .AddTo(subscriptions);
        }

        private void OnDisable()
        {
            subscriptions?.Clear();
            cts?.Cancel();
            cts?.Dispose();
            fadeCts?.Cancel();
            fadeCts?.Dispose();

            appear = 0f;
            if (fogMaterial != null) fogMaterial.SetFloat(DensityId, 0f);
        }

        public void SetFogEnabled(bool value)
        {
            fogEnabled = value;
            if (!value) appearTimer = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (fogEnabled)
            {
                appearTimer += dt;
                if (appearTimer > appearDelay)
                    appear = Mathf.MoveTowards(appear, 1f, dt / appearDuration);
            }
            else
            {
                appear = Mathf.MoveTowards(appear, 0f, dt / appearDuration);
            }

            shownDensityMultiplier = Mathf.SmoothDamp(shownDensityMultiplier, activeDensityMultiplier, ref densityVelocity, multiplierSmoothTime, float.PositiveInfinity, dt);
            shownHeightMaskLengthMultiplier = Mathf.SmoothDamp(shownHeightMaskLengthMultiplier, activeHeightMaskLengthMultiplier, ref heightVelocity, multiplierSmoothTime, float.PositiveInfinity, dt);

            Apply();
        }

        private void OnTimeChanged(float t)
        {
            normalizedTime = t;
        }

        private void Apply()
        {
            if (fogMaterial == null) return;

            float ease = appear * appear * (3f - 2f * appear);

            float density = densityCurve.Evaluate(normalizedTime) * shownDensityMultiplier * currentVisibility * ease;
            float heightLength = heightMaskLengthCurve.Evaluate(normalizedTime) * shownHeightMaskLengthMultiplier * Mathf.Lerp(appearStartHeight, 1f, ease);

            fogMaterial.SetFloat(DensityId, density);
            fogMaterial.SetFloat(HeightMaskLengthId, heightLength);
        }

        private void OnDayNightChanged(bool day)
        {
            if (!day && wasDay)
            {
                activeDensityMultiplier = pendingDensityMultiplier;
                pendingDensityMultiplier = GenerateDensityMultiplier();

                activeHeightMaskLengthMultiplier = pendingHeightMaskLengthMultiplier;
                pendingHeightMaskLengthMultiplier = GenerateDensityMultiplier();;
            }

            wasDay = day;

            if (disableAtNight)
            {
                fadeCts?.Cancel();
                fadeCts = new CancellationTokenSource();
                FadeVisibility(day ? 1f : 0f, fadeCts.Token).Forget();
            }
        }

        private float GenerateDensityMultiplier()
        {
            float min = 0f;
            float max = 1f;
            
            float u = UnityEngine.Random.value;
            float centered = u - 0.5f;
            float uShape = centered * centered * 4f;
            
            return math.lerp(min, max, uShape);
        }
        public void SetPendingDensityMultiplier(float multiplier)
        {
            pendingDensityMultiplier = Mathf.Clamp01(multiplier);
        }

        private async UniTaskVoid FadeVisibility(float target, CancellationToken token)
        {
            try
            {
                float start = currentVisibility;
                float elapsed = 0f;

                while (elapsed < fadeDuration && !token.IsCancellationRequested)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / fadeDuration);
                    float ease = 1f - Mathf.Pow(1f - t, 3f);
                    currentVisibility = Mathf.Lerp(start, target, ease);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!token.IsCancellationRequested)
                    currentVisibility = target;
            }
            catch (OperationCanceledException) { }
        }
    }
}