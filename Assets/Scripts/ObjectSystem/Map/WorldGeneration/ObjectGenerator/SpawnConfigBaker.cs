using Unity.Collections;
using Unity.Mathematics;

namespace TheRavine.Generator
{
    public static class SpawnConfigBaker
    {
        public static NativeArray<ObjectSpawnConfig> BakeSpawnConfigs(
            ObjectSpawnProfileSO[] profiles,
            Allocator allocator,
            out int[] ringConfigEnd)
        {
            ringConfigEnd = new int[ObjectInfo.MaxVisibleRings + 1];

            int count = 0;
            int[] order = new int[profiles?.Length ?? 0];
            for (int i = 0; i < order.Length; i++)
                if (profiles[i] != null && profiles[i].objectInfo != null)
                    order[count++] = i;

            for (int i = 1; i < count; i++)
            {
                int item = order[i];
                int rings = profiles[item].objectInfo.VisibleRings;
                int j = i - 1;
                while (j >= 0 && profiles[order[j]].objectInfo.VisibleRings < rings)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = item;
            }

            var configs = new NativeArray<ObjectSpawnConfig>(count, allocator);

            for (int k = 0; k < count; k++)
            {
                var p = profiles[order[k]];
                byte rings = p.objectInfo.VisibleRings;

                configs[k] = new ObjectSpawnConfig
                {
                    prefabID = p.objectInfo.Id,
                    density = p.density,
                    minDistance = p.minDistance,
                    radiusCells = (int)math.ceil(math.max(p.minDistance, 0f) / MapGenerator.scale),
                    layer = (byte)p.layer,
                    visibleRings = rings,
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

                for (int r = 0; r <= rings; r++)
                    ringConfigEnd[r] = k + 1;
            }

            return configs;
        }
    }
}
