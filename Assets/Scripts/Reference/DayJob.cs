using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Jobs;

namespace TheRavine.Base
{
    [BurstCompile]
    public struct DayJob : IJob
    {
        public NativeArray<float> timeBridge;
        [WriteOnly] public NativeArray<bool> isDayBridge;

        public void Execute()
        {
            timeBridge[0] += DayConstants.DeltaTime / DayConstants.TimeScale * timeBridge[4];
            if (timeBridge[0] > 1f) timeBridge[0] = 0f;

            bool isCurrentlyDay = timeBridge[0] >= DayConstants.DayStart && 
                                timeBridge[0] <= DayConstants.DayEnd;
            isDayBridge[0] = isCurrentlyDay;

            if (isCurrentlyDay)
            {
                CalculateSunParameters();
            }
        }

        private void CalculateSunParameters()
        {
            float normalizedTime = timeBridge[0] - DayConstants.DayStart;
            float angle = normalizedTime * DayConstants.AngleMultiplier;
            
            timeBridge[1] = -math.cos(angle) * DayConstants.SunDistance;
            timeBridge[2] = -math.sin(angle) * DayConstants.SunDistance;
            
            float timeFactor = timeBridge[0] * timeBridge[0] - timeBridge[0];
            timeBridge[3] = -(DayConstants.IntensityFactor * timeFactor + DayConstants.IntensityOffset);
            timeBridge[5] = DayConstants.ShadowScaleBase * timeFactor + DayConstants.ShadowScaleOffset;
        }
    }

    public static class DayConstants
    {
        public const float IntensityFactor = 12.5f;
        public const float IntensityOffset = 1.95f;
        public const float TimeScale = 600f;
        public const float DayStart = 0.2f;
        public const float DayEnd = 0.8f;
        public const float DeltaTime = 0.02f;
        public const float AngleMultiplier = 5f;
        public const float SunDistance = 300f;
        public const float ShadowScaleBase = 7f;
        public const float ShadowScaleOffset = 2.5f;
    }
}