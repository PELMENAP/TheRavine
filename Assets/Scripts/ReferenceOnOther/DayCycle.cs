using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering.Universal;
using Unity.Jobs;
using Unity.Burst;
using Unity.Netcode;
using Unity.Mathematics;
using Unity.Collections;
using Cysharp.Threading.Tasks;


using TheRavine.Services;

namespace TheRavine.Base
{
    [RequireComponent(typeof(Light2D))]
    public class DayCycle : NetworkBehaviour, ISetAble
    {
        public static bool closeThread;
        private static System.Action newDay;
        private NetworkVariable<bool> isDay = new(writePerm: NetworkVariableWritePermission.Server);
        public bool IsDay 
        {
            get {return isDay.Value;} 
            private set {isDay.Value = value;}
        } 
        [SerializeField] private float startDay, speed;
        [SerializeField] private Gradient sunGradient;
        [SerializeField] private int awakeDelay, defaultDelay, _maxLightDistance;
        private NetworkVariable<Color> sunColor = new NetworkVariable<Color>();
        private NetworkVariable<Vector3> sunPosition = new NetworkVariable<Vector3>();
        private NetworkVariable<float> sunIntensity = new NetworkVariable<float>();
        private Light2D sun;
        private NativeArray<float> TimeBridge, Light2DIntensityBridge;
        private NativeArray<bool> IsdayBridge;
        private NativeArray<float3> Light2DBridge;
        private TransformAccessArray shadowsTransform;
        private Transform[] lightsTransform;
        private Transform player;
        public void SetUp(ISetAble.Callback callback, ServiceLocator locator)
        {  
            player = locator.GetPlayerTransform();

            if(!IsServer) return;

            sun = this.GetComponent<Light2D>();
            sunColor.Value = sun.color;
            sunPosition.Value = sun.transform.localPosition;
            sunIntensity.Value = sun.intensity;
            TimeBridge = new NativeArray<float>(6, Allocator.Persistent);
            IsdayBridge = new NativeArray<bool>(1, Allocator.Persistent);
            closeThread = true;
            newDay += GetLightsAndShadows;

            UpdateDay().Forget();

            sunColor.OnValueChanged += OnSunColorChanged;
            sunPosition.OnValueChanged += OnSunPositionChanged;
            sunIntensity.OnValueChanged += OnSunIntensityChanged;
            callback?.Invoke();
        }
        private void GetLightsAndShadows()
        {
            if (shadowsTransform.isCreated)
            {
                shadowsTransform.Dispose();
                Light2DBridge.Dispose();
                Light2DIntensityBridge.Dispose();
            }

            TimeBridge[0] = startDay;
            TimeBridge[4] = speed;
            GameObject[] shadowsObjects = GameObject.FindGameObjectsWithTag("Shadow");
            if (Settings.isShadow)
            {
                Light2D[] lights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);
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
                Light2DBridge[i] = (float3)lightsTransform[i].position;
            }
        }

        private void OnSunColorChanged(Color oldColor, Color newColor)
        {
            sun.color = newColor;
        }

        private void OnSunPositionChanged(Vector3 oldPosition, Vector3 newPosition)
        {
            this.transform.localPosition = newPosition;
        }

        private void OnSunIntensityChanged(float oldIntensity, float newIntensity)
        {
            sun.intensity = newIntensity;
        }

        public void UpdateSunValues()
        {
            sunColor.Value = sunGradient.Evaluate(TimeBridge[0]);
            sunPosition.Value = new Vector3(TimeBridge[1], TimeBridge[2], 0);
            sunIntensity.Value = TimeBridge[3];
        }
        private DayJob dayJob;
        private ShadowsJob shadowJob;
        private JobHandle dayJobHandle, shadowJobHandle;
        private async UniTaskVoid UpdateDay()
        {
            await UniTask.Delay(awakeDelay);
            GetLightsAndShadows();
            while (!DataStorage.sceneClose)
            {
                if(!TimeBridge.IsCreated) break;
                dayJobHandle = dayJob.Schedule();
                UpdateSunValues();
                if (isDay.Value != IsdayBridge[0])
                {
                    isDay.Value = IsdayBridge[0];
                    if (!IsdayBridge[0])
                    {
                        newDay?.Invoke();
                    }
                    await UniTask.Delay(defaultDelay);
                }
                if (Settings.isShadow && shadowsTransform.isCreated)
                {
                    UpdateProperties();
                    shadowJob = new ShadowsJob()
                    {
                        localScale = TimeBridge[5],
                        lightsBridge = Light2DBridge,
                        lightsIntensity = Light2DIntensityBridge,
                        playerPosition = player.position,
                        maxLightDistance = _maxLightDistance
                    };
                    shadowJobHandle = shadowJob.Schedule(shadowsTransform);
                }
                await UniTask.WaitForFixedUpdate();
                dayJobHandle.Complete();
                shadowJobHandle.Complete();
            }
            await UniTask.Delay(defaultDelay);
        }

        public void BreakUp(ISetAble.Callback callback)
        {
            TimeBridge.Dispose();
            IsdayBridge.Dispose();
            newDay -= GetLightsAndShadows;
            sunColor.OnValueChanged -= OnSunColorChanged;
            sunPosition.OnValueChanged -= OnSunPositionChanged;
            sunIntensity.OnValueChanged -= OnSunIntensityChanged;

            callback?.Invoke();
        }

        private void OnDisable() {
            if (Settings.isShadow)
            {
                Light2DBridge.Dispose();
                Light2DIntensityBridge.Dispose();
                shadowsTransform.Dispose();
            }
        }

        private const float _intensityFactor = 12.5f, _intensityOffset = 1.95f;

        [BurstCompile]
        public struct DayJob : IJob
        {
            [ReadOnly] public float deltaTime;
            public NativeArray<float> timeBridge;
            [WriteOnly] public NativeArray<bool> isdayBridge;
            public void Execute()
            {
                timeBridge[0] += deltaTime / 600 * timeBridge[4]; // speed
                if (timeBridge[0] > 1f)
                    timeBridge[0] = 0f;
                if (timeBridge[0] >= 0.2f && timeBridge[0] <= 0.8f)
                {
                    float angle = (timeBridge[0] - 0.2f) * 5;
                    timeBridge[1] = -math.cos(angle) * 300;
                    timeBridge[2] = -math.sin(angle) * 300;
                    float xFactor = timeBridge[0] * timeBridge[0] - timeBridge[0];
                    timeBridge[3] = -(_intensityFactor * xFactor + _intensityOffset); // sun intensity
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
            [ReadOnly] public NativeArray<float> lightsIntensity;
            [ReadOnly] public float localScale, maxLightDistance;
            [ReadOnly] public float3 playerPosition;
            public void Execute(int index, TransformAccess shadowTransform)
            {
                if(math.distancesq(shadowTransform.position, playerPosition) > maxLightDistance) return;
                float maxWeight = 0, lightWeight;
                int lightIndex = 0;
                for (int i = 0; i < lightsBridge.Length; i++)
                {
                    lightWeight = lightsIntensity[i] / math.distancesq((float3)shadowTransform.position, lightsBridge[i]);
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