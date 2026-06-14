using UnityEngine;

namespace TheRavine.Generator
{
    [CreateAssetMenu(menuName = "TheRavine/Generation/Object Spawn Profile")]
    public class ObjectSpawnProfileSO : ScriptableObject
    {
        public SpawnLayer layer;
        
        [Header("Spawn Parameters")]
        [Tooltip("Objects per 100x100 world units")]
        public float density = 10f;
        
        [Tooltip("Minimum distance between objects of same type")]
        public float minDistance = 2f;
        
        [Header("Filters")]
        public DensityMaskAuthoring mask;
        
        [Header("Clustering")]
        public ClusterSettingsAuthoring clusters;
        
        [Header("Prefab Binding")]
        public ObjectInfo objectInfo;
    }
}