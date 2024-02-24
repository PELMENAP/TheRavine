using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using System.Collections;
using UnityEngine.Rendering.Universal;
using Cysharp.Threading.Tasks;
using System;

using TheRavine.Services;

namespace TheRavine.Base
{
    public class DayCycle : MonoBehaviour, ISetAble
    {
        private ServiceLocator serviceLocator;
        public static bool isday, closeThread;
        public static Action newDay;
        [SerializeField] private float startDay, speed;
        [SerializeField] private Gradient sunGradient;
        private Light2D sun;
        private NativeArray<float> TimeBridge, Light2DIntensityBridge;
        private NativeArray<bool> IsdayBridge;
        private NativeArray<Vector3> Light2DBridge;
        private TransformAccessArray shadowsTransform;
        private Transform[] shadows, lightsTransform;
        DayJob dayJob;
        ShadowsJob shadowJob;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            serviceLocator = locator;
            TimeBridge = new NativeArray<float>(5, Allocator.Persistent);
            IsdayBridge = new NativeArray<bool>(1, Allocator.Persistent);
            sun = this.GetComponent<Light2D>();
            newDay += GetLightsAndShadows;
            closeThread = true;
            UpdateDay().Forget();
            callback?.Invoke();
        }

        private void GetLightsAndShadows()
        {
            TimeBridge[0] = startDay;
            TimeBridge[4] = speed;
            GameObject[] shadowsObjects = GameObject.FindGameObjectsWithTag("Shadow");
            if (Settings.isShadow)
            {
                Light2D[] lights = GameObject.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
                shadows = new Transform[shadowsObjects.Length];
                for (ushort i = 0; i < shadowsObjects.Length; i++)
                {
                    shadowsObjects[i].SetActive(true);
                    shadows[i] = shadowsObjects[i].transform;
                }
                shadowsTransform = new TransformAccessArray(shadows);
                Light2DBridge = new NativeArray<Vector3>(lights.Length, Allocator.Persistent);
                Light2DIntensityBridge = new NativeArray<float>(lights.Length, Allocator.Persistent);
                lightsTransform = new Transform[lights.Length];
                for (ushort i = 0; i < lights.Length; i++)
                {
                    lightsTransform[i] = lights[i].transform;
                    Light2DIntensityBridge[i] = lights[i].intensity;
                    if (lights[i].transform.gameObject.CompareTag("Sun"))
                    {
                        Light2DIntensityBridge[i] = 100;
                    }
                }
                UpdateProperties();
            }
            else
            {
                for (ushort i = 0; i < shadowsObjects.Length; i++)
                    shadowsObjects[i].SetActive(false);
            }
            dayJob = new DayJob()
            {
                deltaTime = Time.deltaTime,
                timeBridge = TimeBridge,
                isdayBridge = IsdayBridge
            };
            shadowJob = new ShadowsJob()
            {
                lightsBridge = Light2DBridge,
                ligthsIntensity = Light2DIntensityBridge
            };
        }

        private void UpdateProperties()
        {
            for (ushort i = 0; i < lightsTransform.Length; i++)
            {
                Light2DBridge[i] = lightsTransform[i].position;
            }
        }

        private void UpdateSunValues()
        {
            sun.color = sunGradient.Evaluate(TimeBridge[0]);
            sun.transform.localPosition = new Vector3(TimeBridge[1], TimeBridge[2], 0);
            sun.intensity = TimeBridge[3];
        }

        private async UniTaskVoid UpdateDay()
        {
            await UniTask.Delay(3000);
            GetLightsAndShadows();
            await UniTask.Delay(1000);
            while (closeThread)
            {
                JobHandle dayHande = dayJob.Schedule();
                dayHande.Complete();
                UpdateSunValues();
                if (isday != IsdayBridge[0])
                {
                    isday = IsdayBridge[0];
                    if (!IsdayBridge[0])
                    {
                        newDay?.Invoke();
                    }
                    await UniTask.Delay(1000);
                }
                if (Settings.isShadow)
                {
                    UpdateProperties();
                    JobHandle shadowsHande = shadowJob.Schedule(shadowsTransform);
                    shadowsHande.Complete();
                }
                await UniTask.WaitForFixedUpdate();
            }
            await UniTask.Delay(1000);
            BreakUp();
        }

        public void BreakUp()
        {
            closeThread = false;
            TimeBridge.Dispose();
            IsdayBridge.Dispose();
            Light2DBridge.Dispose();
            Light2DIntensityBridge.Dispose();
            shadowsTransform.Dispose();
        }
        [BurstCompile]
        public struct DayJob : IJob
        {
            [ReadOnly] public float deltaTime;
            public NativeArray<float> timeBridge;
            [WriteOnly] public NativeArray<bool> isdayBridge;
            public void Execute()
            {
                timeBridge[0] += (deltaTime / 600) * timeBridge[4];
                if (timeBridge[0] > 1f)
                    timeBridge[0] = 0f;
                if (timeBridge[0] >= 0.2f && timeBridge[0] <= 0.8f)
                {
                    timeBridge[1] = -math.cos((timeBridge[0] - 0.2f) / 0.6f * 3) * 200;
                    timeBridge[2] = -math.sin((timeBridge[0] - 0.2f) / 0.6f * 3) * 200;
                    timeBridge[3] = (-280 / 9 * timeBridge[0] * timeBridge[0] + 280 / 9 * timeBridge[0] - 43 / 9 - 0.8f) / 2;
                    isdayBridge[0] = true;
                }
                else
                    isdayBridge[0] = false;
            }
        }

        [BurstCompile]
        public struct ShadowsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<Vector3> lightsBridge;
            [ReadOnly] public NativeArray<float> ligthsIntensity;
            private ushort lightIndex;
            private float maxWeight, lightWeight;
            private Vector3 shadowPosition, direction;
            public void Execute(int index, TransformAccess shadowTransform)
            {
                shadowPosition = shadowTransform.position;
                maxWeight = 0;
                for (ushort i = 0; i < lightsBridge.Length; i++)
                {
                    lightWeight = ligthsIntensity[i] / Vector3.Distance(shadowPosition, lightsBridge[i]);
                    if (lightWeight > maxWeight)
                    {
                        maxWeight = lightWeight;
                        lightIndex = i;
                    }
                }
                direction = shadowPosition - lightsBridge[lightIndex];
                shadowTransform.rotation = Quaternion.Euler(50, 0, math.degrees(math.atan2(direction.y, direction.x)) - 90);
            }
        }
    }
}