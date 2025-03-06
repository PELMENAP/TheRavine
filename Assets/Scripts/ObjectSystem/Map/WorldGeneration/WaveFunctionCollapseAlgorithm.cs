using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    public class Cell
    {
        public List<TileRuleSO> PossibleTiles { get; private set; }
        public TileRuleSO CollapsedTile { get; private set; }
        public Vector2Int Position { get; private set; }
        public bool IsCollapsed => CollapsedTile != null;
        
        public Cell(Vector2Int position, List<TileRuleSO> allPossibleTiles)
        {
            Position = position;
            PossibleTiles = new List<TileRuleSO>(allPossibleTiles);
        }
        
        public void Collapse(TileRuleSO tile)
        {
            Debug.Log("cell at " + Position + " was collapsed");
            CollapsedTile = tile;
            PossibleTiles.Clear();
            PossibleTiles.Add(tile);
        }
        public bool ConstrainPossibilities(List<TileRuleSO> allowedTiles)
        {
            if (IsCollapsed) return false;
            
            int initialCount = PossibleTiles.Count;
            
            var previousTiles = new HashSet<TileRuleSO>(PossibleTiles);
            PossibleTiles = PossibleTiles.Intersect(allowedTiles).ToList();
            
            if (PossibleTiles.Count == initialCount)
            {
                var currentTiles = new HashSet<TileRuleSO>(PossibleTiles);
                return !previousTiles.SetEquals(currentTiles);
            }
            
            return true;
        }
        
        public TileRuleSO SelectRandomTileWithWeights(FastRandom random)
        {
            if (PossibleTiles.Count == 0) return null;
            if (PossibleTiles.Count == 1) return PossibleTiles[0];
            
            int totalWeight = PossibleTiles.Sum(t => t.weight);
            if (totalWeight <= 0) return PossibleTiles[random.Next(PossibleTiles.Count)];
            
            int randomWeight = random.Next(totalWeight);
            int accumulatedWeight = 0;
            
            foreach (var tile in PossibleTiles)
            {
                accumulatedWeight += tile.weight;
                if (accumulatedWeight > randomWeight)
                    return tile;
            }
            
            return PossibleTiles[0];
        }
        
        public float GetEntropy()
        {
            if (IsCollapsed) return float.MaxValue;
            if (PossibleTiles.Count == 0) return float.MaxValue;
            
            float weightSum = PossibleTiles.Sum(t => t.weight);
            float weightedEntropy = 0;
            
            foreach (var tile in PossibleTiles)
            {
                float probability = tile.weight / weightSum;
                weightedEntropy -= probability * Mathf.Log(probability);
            }
            
            return weightedEntropy;
        }
    }

    public class WaveFunctionCollapseAlgorithm
    {
        private Dictionary<Vector2Int, Cell> _cells;
        private List<TileRuleSO> _allTiles;
        private GenerationSettingsSO _settings;
        private FastRandom _random;
        private Dictionary<TileRuleSO, Dictionary<Direction, HashSet<TileRuleSO>>> _neighborRulesCache;
        private Dictionary<Vector2Int, GameObject> _result;
        private PriorityQueue<Cell, float> _entropyQueue;
        private Stack<(Vector2Int, List<TileRuleSO>)> _backtrackStack;

        public WaveFunctionCollapseAlgorithm(List<TileRuleSO> allTiles, GenerationSettingsSO settings, int seed = 0)
        {
            _allTiles = allTiles;
            _settings = settings;
            _random = seed == 0 ? new FastRandom() : new FastRandom(seed);
            _result = new Dictionary<Vector2Int, GameObject>();
            _neighborRulesCache = new Dictionary<TileRuleSO, Dictionary<Direction, HashSet<TileRuleSO>>>();
            _cells = new Dictionary<Vector2Int, Cell>();
            _entropyQueue = new PriorityQueue<Cell, float>();
            _backtrackStack = new Stack<(Vector2Int, List<TileRuleSO>)>();
            
            InitializeNeighborRulesCache();
        }

        private void InitializeNeighborRulesCache()
        {
            foreach (var tile in _allTiles)
            {
                var directionRules = new Dictionary<Direction, HashSet<TileRuleSO>>();
                
                foreach (Direction direction in Enum.GetValues(typeof(Direction)))
                {
                    var rule = tile.rules.FirstOrDefault(r => r.direction == direction);
                    var allowedNeighbors = rule != null ? new HashSet<TileRuleSO>(rule.allowedNeighbors) : new HashSet<TileRuleSO>(_allTiles);
                    directionRules[direction] = allowedNeighbors;
                }
                
                _neighborRulesCache[tile] = directionRules;
            }
        }

        private Cell GetCellWithMinimumEntropy()
        {
            while (_entropyQueue.Count > 0)
            {
                var cell = _entropyQueue.Peek();
                if (!cell.IsCollapsed) return _entropyQueue.Dequeue();
                _entropyQueue.Dequeue();
            }
            return null;
        }

        private Vector2Int GetNeighborPosition(Vector2Int position, Direction direction)
        {
            Vector2Int neighborPos = direction switch
            {
                Direction.Up => new Vector2Int(position.x, position.y + 1),
                Direction.Right => new Vector2Int(position.x + 1, position.y),
                Direction.Down => new Vector2Int(position.x, position.y - 1),
                Direction.Left => new Vector2Int(position.x - 1, position.y),
                _ => position
            };
            
            if (_settings.borderRule == GenerationSettingsSO.BorderRuleType.Wrap)
            {
                neighborPos.x = (neighborPos.x + _settings.gridSize.x) % _settings.gridSize.x;
                neighborPos.y = (neighborPos.y + _settings.gridSize.y) % _settings.gridSize.y;
            }
            else if (_settings.borderRule == GenerationSettingsSO.BorderRuleType.Block)
            {
                if (neighborPos.x < 0 || neighborPos.x >= _settings.gridSize.x ||
                    neighborPos.y < 0 || neighborPos.y >= _settings.gridSize.y)
                {
                    return position;
                }
            }

            // there are exist the 3 type - open, when no borders
            
            return neighborPos;
        }

        private List<TileRuleSO> GetAllowedTilesForDirection(Cell cell, Direction direction)
        {
            HashSet<TileRuleSO> allowedTiles = new HashSet<TileRuleSO>();

            foreach (var possibleTile in cell.PossibleTiles)
            {
                if (_neighborRulesCache.TryGetValue(possibleTile, out var directionRules) &&
                    directionRules.TryGetValue(direction, out var allowedNeighbors))
                {
                    allowedTiles.UnionWith(allowedNeighbors);
                }
            }
            return allowedTiles.ToList();
        }

        
        private bool Step()
        {
            Cell cellToCollapse = _settings.useMinimumEntropy ? GetCellWithMinimumEntropy() : _cells.Values.FirstOrDefault(c => !c.IsCollapsed);
            if (cellToCollapse == null) return false;
            _backtrackStack.Push((cellToCollapse.Position, new List<TileRuleSO>(cellToCollapse.PossibleTiles)));
            if (cellToCollapse.PossibleTiles.Count == 0) return false;
            
            var selectedTile = cellToCollapse.SelectRandomTileWithWeights(_random);
            cellToCollapse.Collapse(selectedTile);
            
            return PropagateConstraints(cellToCollapse.Position);
        }
        private bool PropagateConstraints(Vector2Int startPos)
        {
            Queue<Vector2Int> propagationQueue = new Queue<Vector2Int>();
            propagationQueue.Enqueue(startPos);
            
            HashSet<Vector2Int> processedPositions = new HashSet<Vector2Int>(); // Отслеживаем обработанные позиции
            
            int iterations = 0;
            while (propagationQueue.Count > 0 && iterations < _settings.maxPropagationIterations)
            {
                iterations++;
                Vector2Int currentPos = propagationQueue.Dequeue();
                if (!_cells.TryGetValue(currentPos, out var currentCell)) continue;
                
                processedPositions.Add(currentPos); // Отмечаем позицию как обработанную
                
                foreach (Direction direction in Enum.GetValues(typeof(Direction)))
                {
                    Vector2Int neighborPos = GetNeighborPosition(currentPos, direction);
                    if (neighborPos == currentPos) continue; // Пропускаем, если это та же самая позиция
                    
                    if (!_cells.ContainsKey(neighborPos)) 
                        _cells[neighborPos] = new Cell(neighborPos, _allTiles);
                    
                    var neighborCell = _cells[neighborPos];
                    
                    // Получаем допустимые плитки для соседа, исходя из текущей ячейки
                    var allowedNeighborTiles = GetAllowedTilesForDirection(currentCell, direction);
                    
                    // Применяем ограничения
                    bool changed = neighborCell.ConstrainPossibilities(allowedNeighborTiles);
                    
                    // Проверяем на противоречия
                    if (neighborCell.PossibleTiles.Count == 0)
                        return false; // Противоречие, требуется откат
                    
                    // Добавляем соседа в очередь, если его состояние изменилось и он еще не был обработан после последнего изменения
                    if (changed && !processedPositions.Contains(neighborPos))
                    {
                        _entropyQueue.Enqueue(neighborCell, neighborCell.GetEntropy());
                        propagationQueue.Enqueue(neighborPos);
                        // Удаляем из обработанных, так как состояние изменилось и нужно обработать заново
                        processedPositions.Remove(neighborPos);
                    }
                }
            }
            
            // Проверка на достижение максимального числа итераций
            return iterations < _settings.maxPropagationIterations;
        }


        private async UniTask<bool> RunAlgorithmWithBacktracking(CancellationToken cancellationToken)
        {
            int attempts = 0;
            while (attempts < _settings.maxGenerationAttempts)
            {
                attempts++;
                _cells.Clear();
                _backtrackStack.Clear();
                _result.Clear();
                _entropyQueue.Clear();
                
                Vector2Int startPos = new Vector2Int(_random.Next(_settings.gridSize.x), _random.Next(_settings.gridSize.y));
                TileRuleSO initialTile = _allTiles[_random.Next(_allTiles.Count)];
                
                _cells[startPos] = new Cell(startPos, _allTiles);
                _cells[startPos].Collapse(initialTile);
                
                bool success = PropagateConstraints(startPos);
                int stepCount = 0;

                Debug.Log("current attempt: " + attempts + "  " + success);
                
                while (success && stepCount < _settings.maxGeneratedCells)
                {
                    stepCount++;

                    Debug.Log("current step: " + stepCount);

                    if (_settings.generationDelay > 0) await UniTask.Delay((int)(_settings.generationDelay * 1000), cancellationToken: cancellationToken);

                    success = Step();
                    
                    if (!success && _backtrackStack.Count > 0) success = await Backtrack(cancellationToken);
                    if (_cells.Values.All(c => c.IsCollapsed)) 
                    {
                        CreateResultObjects();
                        return true;
                    }
                    if (cancellationToken.IsCancellationRequested) return false;
                }
            }
            return false;
        }
        private void CreateResultObjects()
        {
            foreach (var cell in _cells.Values)
            {
                if (cell.IsCollapsed)
                {
                    _result[cell.Position] = cell.CollapsedTile.prefab;
                }
            }
        }
        private async UniTask<bool> Backtrack(CancellationToken cancellationToken)
        {
            int backtrackDepth = 0;
            const int maxBacktrackDepth = 10;
            
            while (_backtrackStack.Count > 0 && backtrackDepth < maxBacktrackDepth)
            {
                backtrackDepth++;
                var (position, previousPossibleTiles) = _backtrackStack.Pop();
                if (previousPossibleTiles.Count == 0) continue;
                
                // Выбираем плитку и удаляем её из списка возможных
                var selectedTile = previousPossibleTiles[_random.Next(previousPossibleTiles.Count)];
                previousPossibleTiles.Remove(selectedTile);
                
                // Сохраняем оставшиеся возможности для будущих откатов
                if (previousPossibleTiles.Count > 0) 
                    _backtrackStack.Push((position, previousPossibleTiles));
                
                // Очищаем ячейки, которые могли быть затронуты после последнего выбора
                ClearAffectedCells(position);
                
                // Создаем новую ячейку и схлопываем её
                _cells[position] = new Cell(position, new List<TileRuleSO> { selectedTile });
                _cells[position].Collapse(selectedTile);
                
                // Пробуем распространить ограничения
                if (PropagateConstraints(position))
                    return true;
                
                if (cancellationToken.IsCancellationRequested) return false;

                await UniTask.Yield();
            }
            
            return false;
        }

        // Добавление метода для очистки затронутых ячеек при откате
        private void ClearAffectedCells(Vector2Int position)
        {
            var cellsToKeep = new Dictionary<Vector2Int, Cell>();
            
            foreach (var backtrackEntry in _backtrackStack)
            {
                if (_cells.TryGetValue(backtrackEntry.Item1, out var cell) && cell.IsCollapsed)
                {
                    cellsToKeep[backtrackEntry.Item1] = cell;
                }
            }
            
            _cells.Clear();
            foreach (var entry in cellsToKeep)
            {
                _cells[entry.Key] = entry.Value;
            }
            
            // Перестраиваем очередь энтропии
            _entropyQueue.Clear();
            
            foreach (var cell in _cells.Values)
            {
                if (!cell.IsCollapsed)
                {
                    _entropyQueue.Enqueue(cell, cell.GetEntropy());
                }
            }
        }

        public async UniTask<Dictionary<Vector2Int, GameObject>> Generate(CancellationToken cancellationToken)
        {
            bool success = await RunAlgorithmWithBacktracking(cancellationToken);
            if(!success) CreateResultObjects();
            return _result;
        }
    }
}