using System;
using System.Threading;

using Unity.Netcode;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;

using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.U2D;
using UnityEngine.Rendering.Universal;

using Cysharp.Threading.Tasks;

namespace TheRavine.Base
{
    [RequireComponent(typeof(Light2D))]
    public class DayCycle : NetworkBehaviour, ISetAble
    {
        public event Action OnNewDay;
        public bool IsDay => isDay.Value;
        public void SubmitGroup(Transform[] transforms) => shadowBuffer.SubmitGroup(transforms);
        public void SubmitLight(Transform transformLight, float lightIntensity) => lightBuffer?.SubmitGroup(transformLight, lightIntensity);
        [SerializeField] private float startDay = 0f, speed = 1f;
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private int awakeDelay = 1000, defaultDelay = 100;
        [SerializeField] private int maxLights = 100, maxShadows = 1000;
        
        private NetworkVariable<bool> isDay = new(writePerm: NetworkVariableWritePermission.Server);
        private Light2D sun;

        private NativeArray<float>    timeBridge;
        private NativeArray<bool>     isDayBridge;
        private NativeArray<float3>   lightPosBridge;
        private NativeArray<float>    lightIntBridge;
        
        private CircularTransformBuffer shadowBuffer;
        private CircularLightBuffer lightBuffer;

        private DayJob        dayJob;
        private ShadowsJob    shadowJob;
        private JobHandle     dayHandle, shadowHandle;

        private CancellationTokenSource cts;
        private GameSettings gameSettings;

        public void SetUp(ISetAble.Callback callback)
        {
            gameSettings = ServiceLocator.GetService<SettingsModel>().GameSettings.CurrentValue;
            sun    = GetComponent<Light2D>();
            cts    = new CancellationTokenSource();

            if (IsServer)
            {
                timeBridge = new NativeArray<float>(6, Allocator.Persistent);
                isDayBridge = new NativeArray<bool>(1, Allocator.Persistent);
                UpdateDayLoop(cts.Token).Forget();
                dayJob = new DayJob
                {
                    timeBridge  = timeBridge,
                    isDayBridge = isDayBridge
                };
            }

            callback?.Invoke();
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            cts?.Cancel();

            if (timeBridge.IsCreated)     timeBridge.Dispose();
            if (isDayBridge.IsCreated)    isDayBridge.Dispose();
            if (lightPosBridge.IsCreated) lightPosBridge.Dispose();
            if (lightIntBridge.IsCreated) lightIntBridge.Dispose();
            
            shadowBuffer?.Dispose();
            lightBuffer?.Dispose();

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
                InitializeSceneBuffers();

                while (!token.IsCancellationRequested)
                {
                    if (!timeBridge.IsCreated) break;

                    dayHandle = dayJob.Schedule();
                    dayHandle.Complete();

                    var t = timeBridge[0];
                    bool nowDay = isDayBridge[0];
                    UpdateSunOnServerRPC(t, nowDay);

                    if (isDay.Value != nowDay)
                    {
                        isDay.Value = nowDay;
                    }

                    if (nowDay == false)
                    {
                        OnNewDay?.Invoke();
                        await UniTask.Delay(defaultDelay, cancellationToken: token);
                    }

                    if (shadowBuffer != null && lightBuffer != null)
                    {
                        shadowHandle.Complete();

                        shadowBuffer.Sync();
                        lightBuffer.Sync();
                        
                        if (lightBuffer.HasChanges)
                        {
                            UpdateLightBridges();
                            lightBuffer.ClearChangesFlag();
                        }

                        shadowJob = new ShadowsJob
                        {
                            lightsBridge     = lightPosBridge,
                            lightsIntensity  = lightIntBridge,
                            localScale       = timeBridge[5]
                        };

                        shadowHandle = shadowJob.Schedule(shadowBuffer.AccessArray);
                    }

                    await UniTask.WaitForFixedUpdate(cancellationToken: token);
                }
            }
            catch (OperationCanceledException) {}
            finally
            {
            }
        }

        private void InitializeSceneBuffers()
        {
            timeBridge[0] = startDay;
            timeBridge[4] = speed;

            if (gameSettings.enableShadows)
            {
                shadowBuffer = new CircularTransformBuffer(maxShadows);
                lightBuffer = new CircularLightBuffer(maxLights);
                lightPosBridge = new NativeArray<float3>(maxLights, Allocator.Persistent);
                lightIntBridge = new NativeArray<float>(maxLights, Allocator.Persistent);
                
                lightBuffer.SubmitGroup(this.transform, 100f);
            }
        }

        private void UpdateLightBridges()
        {
            if (lightBuffer == null) return;

            int lightCount = lightBuffer.Count;
            var transforms = lightBuffer.LightTransforms;
            var intensities = lightBuffer.LightIntensities;
            
            for (int i = 0; i < lightCount; i++)
            {
                if (transforms[i] != null)
                {
                    lightPosBridge[i] = transforms[i].position;
                    lightIntBridge[i] = intensities[i];
                }
            }
            
            for (int i = lightCount; i < maxLights; i++)
            {
                lightPosBridge[i] = float3.zero;
                lightIntBridge[i] = 0f;
            }
        }
        [ServerRpc(RequireOwnership = false)]
        private void UpdateSunOnServerRPC(float t, bool isDay)
        {
            if (isDay)
            {
                Color color = sunGradient.Evaluate(t);
                float intensity = timeBridge[3];
                Vector3 position = new(timeBridge[1], timeBridge[2], 0);
                ApplySunPropertiesClientRpc(color, intensity, position);
            }
        }

        [ClientRpc]
        private void ApplySunPropertiesClientRpc(Color color, float intensity, Vector3 position)
        {
            sun.color = color;
            sun.intensity = intensity;
            transform.localPosition = position;
        }
    }
}