using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class SpawnConfigBaker
    {
        public static NativeArray<ObjectSpawnConfig> BakeSpawnConfigs(
            ObjectSpawnProfileSO[] profiles, 
            Allocator allocator)
        {
            if (profiles == null || profiles.Length == 0)
                return new NativeArray<ObjectSpawnConfig>(0, allocator);

            var configs = new NativeArray<ObjectSpawnConfig>(profiles.Length, allocator);

            for (int i = 0; i < profiles.Length; i++)
            {
                var p = profiles[i];
                configs[i] = new ObjectSpawnConfig
                {
                    prefabID = p.objectInfo?.PrefabID ?? -1,
                    density = p.density,
                    minDistance = p.minDistance,
                    layer = (byte)p.layer,
                    heightRange = new float4(p.mask.heightMin, p.mask.heightMax, 0f, 0f),
                    tempRange = new float4(p.mask.tempMin, p.mask.tempMax, 0f, 0f),
                    moistRange = new float4(p.mask.moistMin, p.mask.moistMax, 0f, 0f),
                    noiseScale = p.mask.noiseScale,
                    noiseThreshold = p.mask.noiseThreshold,
                    noiseWeight = p.mask.noiseWeight,
                    useClusters = p.clusters.useClusters,
                    clusterCount = math.max(1, p.clusters.clusterCount),
                    clusterSize = math.max(1, p.clusters.clusterSize),
                    clusterRadius = p.clusters.clusterRadius
                };
            }

            return configs;
        }
    }
}