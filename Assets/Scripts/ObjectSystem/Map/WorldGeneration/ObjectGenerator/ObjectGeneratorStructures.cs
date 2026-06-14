using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;

namespace TheRavine.Generator
{
    [Serializable]
    public struct DensityMaskAuthoring
    {
        [Header("Base Filters")]
        [Range(0f, 1f)] public float heightMin;
        [Range(0f, 1f)] public float heightMax;
        [Range(0f, 1f)] public float tempMin;
        [Range(0f, 1f)] public float tempMax;
        [Range(0f, 1f)] public float moistMin;
        [Range(0f, 1f)] public float moistMax;

        [Header("Noise Overlay")]
        [Tooltip("Scale of the density noise mask")]
        public float noiseScale;
        [Range(0f, 1f)] public float noiseThreshold;
        [Range(0f, 1f)] public float noiseWeight;
    }

    [Serializable]
    public struct ClusterSettingsAuthoring
    {
        public bool useClusters;
        [Min(1)] public int clusterCount;
        [Min(1)] public int clusterSize;
        public float clusterRadius;
    }

    public struct ObjectSpawnConfig
    {
        public int prefabID;
        public float density;
        public float minDistance;
        public byte layer;

        public float4 heightRange; 
        public float4 tempRange;
        public float4 moistRange;

        public float noiseScale;
        public float noiseThreshold;
        public float noiseWeight;

        public bool useClusters;
        public int clusterCount;
        public int clusterSize;
        public float clusterRadius;
    }


    public enum SpawnLayer : byte
    {
        Structures = 0,
        Resources = 1,
        Vegetation = 2,
        Decorations = 3,
        Creatures = 4
    }
}