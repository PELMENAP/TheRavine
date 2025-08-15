using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using UnityEngine;
using UnityEngine.Jobs;

namespace TheRavine.Base
{
    [BurstCompile]
    public struct ShadowsJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float3> lightsBridge;
        [ReadOnly] public NativeArray<float>  lightsIntensity;
        [ReadOnly] public float               localScale;

        public void Execute(int i, TransformAccess shadow)
        {
            if(!shadow.isValid) return;
            int dominantLightIndex = FindDominantLight(shadow.position);
            ApplyShadowTransform(shadow, dominantLightIndex);
        }

        private int FindDominantLight(float3 shadowPos)
        {
            float maxWeight = 0f;
            int bestIndex = 0;
            
            for (int j = 0; j < lightsBridge.Length; j++)
            {
                float distanceSq = math.distancesq(shadowPos, lightsBridge[j]);
                float weight = lightsIntensity[j] / math.max(distanceSq, 0.01f);
                
                if (weight > maxWeight) 
                { 
                    maxWeight = weight; 
                    bestIndex = j; 
                }
            }
            
            return bestIndex;
        }

        private void ApplyShadowTransform(TransformAccess shadow, int lightIndex)
        {
            float3 lightPos = lightsBridge[lightIndex];
            float3 shadowPos = shadow.position;
            float3 direction = math.normalize(shadowPos - lightPos);
            float angle = math.degrees(math.atan2(direction.y, direction.x));
            
            shadow.rotation = Quaternion.Euler(ShadowConstants.XAngle, -angle * ShadowConstants.YAngleFactor, angle - ShadowConstants.ZAngleOffset);
            shadow.localScale = new Vector3(ShadowConstants.DefaultLocalScale, localScale, ShadowConstants.DefaultLocalScale);
        }
    }

    public static class ShadowConstants
    {
        public const float XAngle = 50f;
        public const float YAngleFactor = 0.1f;
        public const float ZAngleOffset = 90f;
        public const float DefaultLocalScale = 1f;
    }
}