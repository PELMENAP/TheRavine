using System;
using System.Threading;

using Unity.Netcode;

using UnityEngine;

using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    [RequireComponent(typeof(Light))]
    public class DayCycle : NetworkBehaviour, ISetAble
    {
        public event Action OnNewDay;
        public bool IsDay => isDay.Value;

        [SerializeField] private float speed = 1f;
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private AnimationCurve xRotationCurve;
        [SerializeField] private float xScale = 50f;
        [SerializeField] private AnimationCurve yRotationCurve;
        [SerializeField] private float yScale = 90f;
        [SerializeField] private AnimationCurve intensityCurve;
        [SerializeField] private AnimationCurve shadowStrengthCurve;
        [SerializeField] private int awakeDelay = 1000;

        private NetworkVariable<bool> isDay = new(writePerm: NetworkVariableWritePermission.Server);
        private Light sun;
        private float t = 0f;

        private CancellationTokenSource cts;
        private GlobalSettings gameSettings;

        public void SetUp(ISetAble.Callback callback)
        {
            ServiceLocator.Services.Register(this);
            
            gameSettings = ServiceLocator.GetService<GlobalSettingsController>().GetCurrent();
            sun = GetComponent<Light>();
            cts = new CancellationTokenSource();

            if (IsServer)
            {
                UpdateDayLoop(cts.Token).Forget();
            }

            callback?.Invoke();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            cts?.Cancel();
            callback?.Invoke();
        }

        private void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
        private async UniTaskVoid UpdateDayLoop(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(awakeDelay, cancellationToken: token);
                while (!token.IsCancellationRequested)
                {
                    t += DayConstants.DeltaTime / DayConstants.TimeScale * speed;
                    if (t > 1f) t = 0f;

                    UpdateSunOnServerRPC(t);

                    bool nowDay = t >= DayConstants.DayStart && t <= DayConstants.DayEnd;
                    if (isDay.Value != nowDay)
                    {
                        isDay.Value = nowDay;
                        if (!nowDay)
                        {
                            OnNewDay?.Invoke();
                        }
                    }

                    await UniTask.WaitForFixedUpdate(cancellationToken: token);
                }
            }
            catch (OperationCanceledException) { }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void UpdateSunOnServerRPC(float t)
        {
            Color color = sunGradient.Evaluate(t);
            float intensity = intensityCurve.Evaluate(t);
            float xAngle = xRotationCurve.Evaluate(t) * xScale;
            float yAngle = yRotationCurve.Evaluate(t) * yScale;
            float shadowStrength = shadowStrengthCurve.Evaluate(t);
            ApplySunPropertiesClientRpc(color, intensity, xAngle, yAngle, shadowStrength);
        }

        [ClientRpc]
        private void ApplySunPropertiesClientRpc(Color color, float intensity, float xAngle, float yAngle, float shadowStrength)
        {
            sun.color = color;
            sun.intensity = intensity;
            sun.shadowStrength = shadowStrength;
            transform.localRotation = Quaternion.Euler(xAngle, yAngle, 0f);
        }
    }
    public static class DayConstants
    {
        public const float TimeScale = 600f;
        public const float DayStart = 0.2f;
        public const float DayEnd = 0.8f;
        public const float DeltaTime = 0.02f;
    }
}