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

        private CancellationTokenSource cts;
        private CancellationTokenSource fadeCts;
        private readonly CompositeDisposable subscriptions = new();

        private float currentVisibility = 1f;
        private bool wasDay;

        private float activeDensityMultiplier = 1f;
        private float pendingDensityMultiplier = 1f;

        private float activeHeightMaskLengthMultiplier = 1f;
        private float pendingHeightMaskLengthMultiplier = 1f;

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
        }

        private void OnTimeChanged(float t)
        {
            float density = densityCurve.Evaluate(t) * activeDensityMultiplier * currentVisibility;

            if(density < 0.01f)
            {
                return;
            }


            float heightLength = heightMaskLengthCurve.Evaluate(t) * activeHeightMaskLengthMultiplier;

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