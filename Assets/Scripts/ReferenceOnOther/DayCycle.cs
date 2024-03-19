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
    [RequireComponent(typeof(Light2D))]
    public class DayCycle : MonoBehaviour, ISetAble
    {
        public static bool isday, closeThread;
        public static Action newDay;
        [SerializeField] private float startDay, speed;
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private int awakeDelay, defaultDelay;
        private Light2D sun;
        private NativeArray<float> TimeBridge, Light2DIntensityBridge;
        private NativeArray<bool> IsdayBridge;
        private NativeArray<float3> Light2DBridge;
        private TransformAccessArray shadowsTransform;
        private Transform[] lightsTransform;
        DayJob dayJob;
        ShadowsJob shadowJob;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {
            TimeBridge = new NativeArray<float>(6, Allocator.Persistent);
            IsdayBridge = new NativeArray<bool>(1, Allocator.Persistent);
            sun = this.GetComponent<Light2D>();
            // newDay += GetLightsAndShadows;
            closeThread = true;
            UpdateDay().Forget();
            GetLightsAndShadows();
            callback?.Invoke();
        }

        private int sunIndex;
        private void GetLightsAndShadows()
        {
            // TimeBridge[0] = startDay;
            TimeBridge[4] = speed;
            GameObject[] shadowsObjects = GameObject.FindGameObjectsWithTag("Shadow");
            if (Settings.isShadow)
            {
                Light2D[] lights = GameObject.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
                Transform[] shadows = new Transform[shadowsObjects.Length];
                for (ushort i = 0; i < shadowsObjects.Length; i++)
                    shadows[i] = shadowsObjects[i].transform;
                shadowsTransform = new TransformAccessArray(shadows);
                Light2DBridge = new NativeArray<float3>(lights.Length, Allocator.Persistent);
                Light2DIntensityBridge = new NativeArray<float>(lights.Length, Allocator.Persistent);
                lightsTransform = new Transform[lights.Length];
                for (ushort i = 0; i < lights.Length; i++)
                {
                    lightsTransform[i] = lights[i].transform;
                    Light2DIntensityBridge[i] = lights[i].intensity;
                    if (lights[i].transform.gameObject.CompareTag("Sun"))
                    {
                        Light2DIntensityBridge[i] = 100;
                        sunIndex = i;
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
        }

        private void UpdateProperties()
        {
            for (ushort i = 0; i < lightsTransform.Length; i++)
            {
                if (sunIndex == i)
                    Light2DBridge[i] = (float3)lightsTransform[i].position;
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
            await UniTask.Delay(awakeDelay);
            // await UniTask.Delay(1000);
            while (!DataStorage.sceneClose)
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
                    await UniTask.Delay(defaultDelay);
                }
                if (Settings.isShadow)
                {
                    UpdateProperties();
                    shadowJob = new ShadowsJob()
                    {
                        localScale = TimeBridge[5],
                        lightsBridge = Light2DBridge,
                        ligthsIntensity = Light2DIntensityBridge
                    };
                    JobHandle shadowHande = shadowJob.Schedule(shadowsTransform);
                    shadowHande.Complete();
                }
                await UniTask.WaitForFixedUpdate();
            }
            await UniTask.Delay(defaultDelay);
            // BreakUp();
        }

        public void BreakUp()
        {
            // newDay -= GetLightsAndShadows;
            TimeBridge.Dispose();
            IsdayBridge.Dispose();
            if (Settings.isShadow)
            {
                Light2DBridge.Dispose();
                Light2DIntensityBridge.Dispose();
                shadowsTransform.Dispose();
            }
        }

        [BurstCompile]
        public struct DayJob : IJob
        {
            [ReadOnly] public float deltaTime;
            public NativeArray<float> timeBridge;
            [WriteOnly] public NativeArray<bool> isdayBridge;
            public void Execute()
            {
                timeBridge[0] += (deltaTime / 600) * timeBridge[4]; // speed
                if (timeBridge[0] > 1f)
                    timeBridge[0] = 0f;
                if (timeBridge[0] >= 0.2f && timeBridge[0] <= 0.8f)
                {
                    float angle = (timeBridge[0] - 0.2f) * 5;
                    timeBridge[1] = -math.cos(angle) * 200;
                    timeBridge[2] = -math.sin(angle) * 200;
                    float xFactor = timeBridge[0] * timeBridge[0] - timeBridge[0];
                    timeBridge[3] = -(12.5f * xFactor + 2f); // sun intensity
                    timeBridge[5] = 7f * xFactor + 2.5f; // shadow scale
                    isdayBridge[0] = true;
                }
                else
                    isdayBridge[0] = false;
            }
        }
        [BurstCompile]
        public struct ShadowsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<float3> lightsBridge;
            [ReadOnly] public NativeArray<float> ligthsIntensity;
            [ReadOnly] public float localScale;
            public void Execute(int index, TransformAccess shadowTransform)
            {
                float maxWeight = 0, lightWeight;
                byte lightIndex = 0;
                for (byte i = 0; i < lightsBridge.Length; i++)
                {
                    lightWeight = ligthsIntensity[i] / math.distancesq((float3)shadowTransform.position, lightsBridge[i]);
                    if (lightWeight > maxWeight)
                    {
                        maxWeight = lightWeight;
                        lightIndex = i;
                    }
                }
                float angle = math.degrees(math.atan2(shadowTransform.position.y - lightsBridge[lightIndex].y, shadowTransform.position.x - lightsBridge[lightIndex].x));
                shadowTransform.rotation = Quaternion.Euler(50, -angle / 10, angle - 90);
                shadowTransform.localScale = new Vector3(1, localScale, 1);
            }
        }
    }
}