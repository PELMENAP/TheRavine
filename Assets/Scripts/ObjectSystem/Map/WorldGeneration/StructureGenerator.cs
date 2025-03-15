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
        [SerializeField] private Vector2Int startPoint;
        private WaveFunctionCollapseAlgorithm _algorithm;
        private Dictionary<Vector2Int, GameObject> _generatedObjects = new Dictionary<Vector2Int, GameObject>();
        private CancellationTokenSource _cancellationTokenSource;

        [Button]
        private void StartGeneration()
        {
            if(_algorithm == null) _algorithm = new WaveFunctionCollapseAlgorithm(_settings);
            Generate(startPoint).Forget();
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

            var result = await _algorithm.Generate(_cancellationTokenSource.Token, triggerPosition, initialTile);

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
                    if (Application.isPlaying)
                        Destroy(obj);
                    else
                        DestroyImmediate(obj);
                }
            }
            
            _generatedObjects.Clear();
        }
    }
}