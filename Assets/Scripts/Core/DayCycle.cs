using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using Unity.Netcode;
using UnityEngine;

namespace TheRavine.Base
{
    [RequireComponent(typeof(Light))]
    public class DayCycle : NetworkBehaviour, ISetAble
    {
        public ReadOnlyReactiveProperty<bool> IsDay => isDay;
        public ReadOnlyReactiveProperty<float> NormalizedTime => normalizedTime;

        [SerializeField] private float speed = 1f;
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private AnimationCurve intensityCurve;
        [SerializeField] private AnimationCurve nightBlendCurve;
        [SerializeField] private Transform secondSun;
        [SerializeField] private Material skyboxMaterial;
        private readonly static int nightBlendId = Shader.PropertyToID("_NightBlend");
        [SerializeField] private int awakeDelay = 1000;
        private readonly float rotationSpeedX = 360f; 

        private readonly NetworkVariable<float> serverTime = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly ReactiveProperty<bool> isDay = new(false);
        private readonly ReactiveProperty<float> normalizedTime = new(0f);

        private Light sun;
        private CancellationTokenSource cts;
        private float localTime;
        private bool wasDay;
        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);
            sun = GetComponent<Light>();
            cts = new CancellationTokenSource();

            if (IsServer)
                ServerLoop(cts.Token).Forget();

            ClientLoop(cts.Token).Forget();
            callback?.Invoke();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            OnDisable();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            serverTime?.Dispose();
            isDay?.Dispose();
            normalizedTime?.Dispose();
            cts?.Cancel();
            cts?.Dispose();
        }

        private async UniTaskVoid ServerLoop(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(awakeDelay, cancellationToken: token);
                while (!token.IsCancellationRequested)
                {
                    float t = (serverTime.Value + DayConstants.DeltaTime / DayConstants.TimeScale * speed) % 1f;
                    serverTime.Value = t;

                    bool nowDay = t >= DayConstants.DayStart && t <= DayConstants.DayEnd;
                    if (wasDay != nowDay)
                        wasDay = nowDay;

                    await UniTask.WaitForFixedUpdate(cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async UniTaskVoid ClientLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    float dt = Time.deltaTime;
                    float diff = serverTime.Value - localTime;

                    if (diff > 0.5f) diff -= 1f;
                    if (diff < -0.5f) diff += 1f;

                    localTime = Mathf.Repeat(localTime + diff * Mathf.Min(dt * 5f, 1f), 1f);
                    normalizedTime.Value = localTime;

                    bool nowDay = localTime >= DayConstants.DayStart && localTime <= DayConstants.DayEnd;
                    isDay.Value = nowDay;

                    ApplyVisuals(localTime);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ApplyVisuals(float t)
        {
            float xAngle = t * rotationSpeedX;
            transform.localRotation = Quaternion.Euler(xAngle, 0, 0);
            secondSun.localRotation = Quaternion.Euler(xAngle - 180f, 0, 0);

            sun.color = sunGradient.Evaluate(t);
            sun.intensity = intensityCurve.Evaluate(t);

            skyboxMaterial.SetFloat(
                nightBlendId,
                nightBlendCurve.Evaluate(t));
        }
    }

    public static class DayConstants
    {
        public const float TimeScale = 600f;
        public const float DayStart = 0.55f;
        public const float DayEnd = 0.95f;
        public const float DeltaTime = 0.02f;
    }
}