using UnityEngine;
using System.Collections.Generic;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "GenerationSettings", menuName = "WFC/Generation Settings")]
    public class GenerationSettingsSO : ScriptableObject
    {
        [Range(1, 10), Tooltip("Максимальное количество попыток генерации до отказа.")]
        public int maxGenerationAttempts = 3;
        
        [Range(1, 1000), Tooltip("Максимальное количество ячеек, которые можно создать.")]
        public int maxGeneratedCells = 50;
        
        [Tooltip("Размер границы, если требуется фиксированный размер.")]
        public Vector2Int gridSize = new(10, 10);
        
        public enum BorderRuleType
        {
            Wrap,  // Карта зацикливается (например, планета)
            Block, // Крайние границы запрещают генерацию
            Open   // Нет границ, генерация идет бесконечно
        }

        [Tooltip("Жадное добавление оставшихся тайлов.")]
        public bool IsGreedyCollapse = true;
        
        [Tooltip("Как генерация обрабатывает края карты.")]
        public BorderRuleType borderRule = BorderRuleType.Block;
        
        [Range(1, 100), Tooltip("Максимальное количество итераций в процессе распространения (propagation).")]
        public int maxPropagationIterations = 20;
        [Range(1, 100), Tooltip("Максимальное количество итераций в шагах генерации.")]
        public int maxStepIterations = 20;
        
        [Range(1, 500), Tooltip("Задержка между шагами генерации в доли секундах.")]
        public int generationDelay = 1;
        [Tooltip("Множество возможных тайлов.")]
        public List<TileRuleSO> _availableTiles;
    }
}