using UnityEngine;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Collections;
using System.Collections;
using UnityEngine.Rendering.Universal;
using System;

public class DayCycle : MonoBehaviour, ISetAble
{
    public static bool isday, closeThread;
    public static event Action newDay;
    [SerializeField] private float startDay, speed;
    [SerializeField] private Gradient sunGradient;
    private Light2D sun;
    private NativeArray<float> TimeBridge;
    private NativeArray<bool> IsdayBridge;


    private NativeArray<Vector3> Light2DBridge;
    private NativeArray<float> Light2DIntensityBridge;

    TransformAccessArray shadowsTransform;

    private Transform[] shadows, lightsTransform;

    public void SetUp()
    {
        GetLightsAndShadows();
    }

    private void GetLightsAndShadows()
    {
        TimeBridge = new NativeArray<float>(5, Allocator.Persistent);
        IsdayBridge = new NativeArray<bool>(1, Allocator.Persistent);
        TimeBridge[0] = startDay;
        TimeBridge[4] = speed;
        sun = this.GetComponent<Light2D>();
        GameObject[] shadowsObjects = GameObject.FindGameObjectsWithTag("Shadow");
        // print(Settings.isShadow);
        if (Settings.isShadow)
        {
            Light2D[] lights = GameObject.FindObjectsByType<Light2D>(FindObjectsSortMode.None);
            shadows = new Transform[shadowsObjects.Length];
            for (int i = 0; i < shadowsObjects.Length; i++)
            {
                shadowsObjects[i].SetActive(true);
                shadows[i] = shadowsObjects[i].transform;
            }
            shadowsTransform = new TransformAccessArray(shadows);

            Light2DBridge = new NativeArray<Vector3>(lights.Length, Allocator.Persistent);
            Light2DIntensityBridge = new NativeArray<float>(lights.Length, Allocator.Persistent);
            lightsTransform = new Transform[lights.Length];
            for (int i = 0; i < lights.Length; i++)
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
            for (int i = 0; i < shadowsObjects.Length; i++)
                shadowsObjects[i].SetActive(false);
        }
        closeThread = true;
        StartCoroutine(UpdateDay());
    }

    private void UpdateProperties()
    {
        for (int i = 0; i < lightsTransform.Length; i++)
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

    private IEnumerator UpdateDay()
    {
        var dayJob = new DayJob()
        {
            timeBridge = TimeBridge,
            isdayBridge = IsdayBridge
        };
        var shadowJob = new ShadowsJob()
        {
            lightsBridge = Light2DBridge,
            ligthsIntensity = Light2DIntensityBridge
        };
        while (closeThread)
        {
            JobHandle dayHande = dayJob.Schedule();
            dayHande.Complete();
            UpdateSunValues();
            if (isday != IsdayBridge[0])
            {
                isday = IsdayBridge[0];
                newDay?.Invoke();
            }
            if (Settings.isShadow)
            {
                UpdateProperties();
                JobHandle shadowsHande = shadowJob.Schedule(shadowsTransform);
                shadowsHande.Complete();
            }
            yield return new WaitForFixedUpdate();
        }
        TimeBridge.Dispose();
        IsdayBridge.Dispose();
        Light2DBridge.Dispose();
        Light2DIntensityBridge.Dispose();
        shadowsTransform.Dispose();
    }

    private void OnDisable()
    {
        TimeBridge.Dispose();
        IsdayBridge.Dispose();
        Light2DBridge.Dispose();
        Light2DIntensityBridge.Dispose();
        shadowsTransform.Dispose();
    }

    public struct DayJob : IJob
    {
        public NativeArray<float> timeBridge;
        public NativeArray<bool> isdayBridge;
        public void Execute()
        {
            timeBridge[0] += (TimeUpdate.globalDeltaTime / 600) * timeBridge[4];
            if (timeBridge[0] > 1f)
            {
                timeBridge[0] = 0f;
            }
            if (timeBridge[0] >= 0.2f && timeBridge[0] <= 0.8f)
            {
                timeBridge[1] = -Mathf.Cos((timeBridge[0] - 0.2f) / 0.6f * 3) * 200;
                timeBridge[2] = -Mathf.Sin((timeBridge[0] - 0.2f) / 0.6f * 3) * 200;
                timeBridge[3] = (-280 / 9 * timeBridge[0] * timeBridge[0] + 280 / 9 * timeBridge[0] - 43 / 9 - 0.8f) / 2;
                isdayBridge[0] = true;
            }
            else
            {
                isdayBridge[0] = false;
            }
        }
    }

    public struct ShadowsJob : IJobParallelForTransform
    {
        [ReadOnly]
        public NativeArray<Vector3> lightsBridge;
        [ReadOnly]
        public NativeArray<float> ligthsIntensity;

        private int lightIndex;
        private float maxWeight, lightWeight;
        private Vector3 shadowPosition, direction;

        public void Execute(int index, TransformAccess shadowTransform)
        {
            shadowPosition = shadowTransform.position;
            maxWeight = 0;
            for (int i = 0; i < lightsBridge.Length; i++)
            {
                lightWeight = ligthsIntensity[i] / Vector3.Distance(shadowPosition, lightsBridge[i]);
                if (lightWeight > maxWeight)
                {
                    maxWeight = lightWeight;
                    lightIndex = i;
                }
            }
            direction = shadowPosition - lightsBridge[lightIndex];
            shadowTransform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
        }
    }
}