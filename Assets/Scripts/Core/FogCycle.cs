using Cysharp.Threading.Tasks;
using System.Threading;
using R3;
using UnityEngine;

namespace TheRavine.Base
{
    public class FogCycle : MonoBehaviour
    {

        [Header("References")]
        [SerializeField] private DayCycle dayCycle;
        [SerializeField] private Material fogMaterial;

        [Header("Color & Density")]
        [SerializeField] private Gradient fogColor = new Gradient();
        [SerializeField] private AnimationCurve densityCurve = AnimationCurve.Constant(0, 1, 0.05f);
        [SerializeField] private AnimationCurve colorIntensityCurve = AnimationCurve.Constant(0, 1, 1f);

        [Header("Height Mask")]
        [SerializeField] private AnimationCurve heightMaskLengthCurve = AnimationCurve.Constant(0, 1, 100f);
        [SerializeField] private AnimationCurve heightMaskFalloffCurve = AnimationCurve.Constant(0, 1, 1f);

        [Header("Night Behaviour")]
        [SerializeField] private AnimationCurve nightVisibilityCurve = AnimationCurve.Constant(0, 1, 1f);
        [SerializeField] private bool disableAtNight = false;
        [SerializeField] private float fadeDuration = 2f;

        private CancellationTokenSource cts;
        private readonly CompositeDisposable subscriptions = new();
        private float currentVisibility = 1f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int HeightMaskLengthId = Shader.PropertyToID("_HeightMaskLength");
        private static readonly int HeightMaskFalloffId = Shader.PropertyToID("_HeightMaskFalloff");

        public void OnEnable()
        {
            cts = new CancellationTokenSource();

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
        }

        private void OnTimeChanged(float normalizedTime)
        {
            if (fogMaterial == null) return;

            float t = normalizedTime;

            Color baseColor = fogColor.Evaluate(t);
            float colorIntensity = colorIntensityCurve.Evaluate(t);
            float visibilityMultiplier = nightVisibilityCurve.Evaluate(t);

            float visibility = visibilityMultiplier * currentVisibility;

            float density = densityCurve.Evaluate(t) * visibility;
            Color finalColor = baseColor * colorIntensity * visibility;

            float heightLength = heightMaskLengthCurve.Evaluate(t);
            float heightFalloff = heightMaskFalloffCurve.Evaluate(t);


            fogMaterial.SetColor(ColorId, finalColor);
            fogMaterial.SetFloat(DensityId, density);
            fogMaterial.SetFloat(HeightMaskLengthId, heightLength);
            fogMaterial.SetFloat(HeightMaskFalloffId, heightFalloff);
        }

        private void OnDayNightChanged(bool day)
        {
            if (disableAtNight)
            {
                FadeVisibility(day ? 1f : 0f, cts.Token).Forget();
            }
        }

        private async UniTaskVoid FadeVisibility(float target, CancellationToken token)
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
            {
                currentVisibility = target;
            }
        }
    }
}