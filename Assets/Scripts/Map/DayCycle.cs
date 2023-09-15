using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;
using System;

public class DayCycle : MonoBehaviour
{
    public static bool isday, closeThread, shadow;
    public static event Action newDay;
    [SerializeField] private float startDay, speed;
    [SerializeField] private Gradient sunGradient;
    [SerializeField] private Transform player;
    private UnityEngine.Rendering.Universal.Light2D sun;
    private NativeArray<float> TimeBridge;
    private NativeArray<bool> IsdayBridge;
    private NativeArray<Vector3> ShadowsBridge;
    private NativeArray<Quaternion> RotationsBridge;
    private GameObject[] shadows;

    private void Start()
    {
        sun = (UnityEngine.Rendering.Universal.Light2D)GetComponent("UnityEngine.Rendering.Universal.Light2D");
        if (shadow)
        {
            shadows = GameObject.FindGameObjectsWithTag("Shadow");
            RotationsBridge = new NativeArray<Quaternion>(shadows.Length, Allocator.Persistent);
            ShadowsBridge = new NativeArray<Vector3>(shadows.Length + 1, Allocator.Persistent);
            ShadowsBridge[0] = sun.transform.position;
            for (int i = 0; i < shadows.Length; i++)
            {
                shadows[i].SetActive(true);
                ShadowsBridge[i + 1] = shadows[i].transform.position;
            }
        }
        closeThread = true;
        StartCoroutine(UpdateDay());
    }

    private IEnumerator UpdateDay()
    {
        TimeBridge = new NativeArray<float>(5, Allocator.Persistent);
        IsdayBridge = new NativeArray<bool>(1, Allocator.Persistent);
        TimeBridge[0] = startDay;
        TimeBridge[4] = speed;
        var dayjob = new DayJob()
        {
            timeBridge = TimeBridge,
            isdayBridge = IsdayBridge
        };
        // JobHandle dayHande = dayjob.Schedule();
        // dayHande.Complete();
        while (closeThread)
        {
            JobHandle dayHande = dayjob.Schedule();
            dayHande.Complete();
            sun.color = sunGradient.Evaluate(TimeBridge[0]);
            sun.transform.position = new Vector3(TimeBridge[1], TimeBridge[2], 0) + player.position;
            sun.intensity = TimeBridge[3];
            if (isday != IsdayBridge[0])
            {
                isday = IsdayBridge[0];
                newDay?.Invoke();
            }
            yield return new WaitForFixedUpdate();
            if (shadow)
            {
                ShadowsBridge[0] = sun.transform.position;
                var shadowJob = new ShadowsJob()
                {
                    shadowsBridge = ShadowsBridge,
                    rotationsBridge = RotationsBridge
                };
                JobHandle shadowsHande = shadowJob.Schedule();
                shadowsHande.Complete();
                for (int i = 0; i < shadows.Length; i++)
                {
                    shadows[i].transform.rotation = RotationsBridge[i];
                }
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
                closeThread = false;
            }
        }
        TimeBridge.Dispose();
        IsdayBridge.Dispose();
        ShadowsBridge.Dispose();
        RotationsBridge.Dispose();
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

    public struct ShadowsJob : IJob
    {
        public NativeArray<Vector3> shadowsBridge;
        public NativeArray<Quaternion> rotationsBridge;
        public void Execute()
        {
            Vector3 direction;
            for (int i = 1; i < shadowsBridge.Length; i++)
            {
                direction = shadowsBridge[i] - shadowsBridge[0];
                rotationsBridge[i - 1] = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90);
            }
        }
    }
}
