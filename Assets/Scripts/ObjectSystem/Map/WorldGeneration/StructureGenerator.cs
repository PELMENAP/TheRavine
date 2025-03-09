using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

using NaughtyAttributes;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public class StructureGenerator : MonoBehaviour
    {
        [SerializeField] private GenerationSettingsSO _settings;
        private WaveFunctionCollapseAlgorithm _algorithm;
        private Dictionary<Vector2Int, GameObject> _generatedObjects = new Dictionary<Vector2Int, GameObject>();
        private CancellationTokenSource _cancellationTokenSource;

        private void Awake()
        {
            _algorithm = new WaveFunctionCollapseAlgorithm(_settings);
        }

        [Button]
        private void StartGeneration()
        {
            Generate(new Vector2Int(0, 0)).Forget();
        }
        
        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        
        public async UniTask<Dictionary<Vector2Int, GameObject>> Generate(Vector2Int triggerPosition, TileRuleSO initialTile = null)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            
            ClearGeneration();
            
            print("start generation");

            var result = await _algorithm.Generate(_cancellationTokenSource.Token);
            
            print("end generation");
            print("количество объектов " + result.Count);

            foreach (var item in result)
            {
                var position = new Vector3(item.Key.x, item.Key.y, 0);
                var prefab = item.Value;
                
                if (prefab != null)
                {
                    var instance = Instantiate(prefab, position, Quaternion.identity, transform);
                    _generatedObjects[item.Key] = instance;
                }
            }
            
            return _generatedObjects;
        }
        
        public void ClearGeneration()
        {
            foreach (var obj in _generatedObjects.Values)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            
            _generatedObjects.Clear();
        }
    }
}