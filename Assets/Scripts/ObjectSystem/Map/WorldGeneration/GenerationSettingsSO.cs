using UnityEngine;

namespace TheRavine.Generator
{
    [CreateAssetMenu(fileName = "GenerationSettings", menuName = "WFC/Generation Settings")]
    public class GenerationSettingsSO : ScriptableObject
    {
        [Tooltip("Максимальное количество попыток генерации до отказа.")]
        public int maxGenerationAttempts = 3;
        
        [Tooltip("Максимальное количество ячеек, которые можно создать.")]
        public int maxGeneratedCells = 50;
        
        [Tooltip("Размер границы, если требуется фиксированный размер.")]
        public Vector2Int gridSize = new Vector2Int(10, 10);
        
        public enum BorderRuleType
        {
            Wrap,  // Карта зацикливается (например, планета)
            Block, // Крайние границы запрещают генерацию
            Open   // Нет границ, генерация идет бесконечно
        }
        
        [Tooltip("Как генерация обрабатывает края карты.")]
        public BorderRuleType borderRule = BorderRuleType.Block;
        
        [Tooltip("Максимальное количество итераций в процессе распространения (propagation).")]
        public int maxPropagationIterations = 2000;
        
        [Range(0.05f, 5f), Tooltip("Задержка между шагами генерации в секундах.")]
        public float generationDelay = 0.1f;
        
        [Tooltip("Разрешить ли генерацию в диагональных направлениях (для полного BFS).")]
        public bool allowDiagonalExpansion = false;
        
        [Tooltip("Использовать ли эвристику минимальной энтропии при выборе клетки.")]
        public bool useMinimumEntropy = true;
    }
}