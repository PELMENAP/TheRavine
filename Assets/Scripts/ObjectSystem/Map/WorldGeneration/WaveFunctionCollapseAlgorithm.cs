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
    }

    public class WaveFunctionCollapseAlgorithm
    {
        private Dictionary<Vector2Int, Cell> _cells;
        private List<TileRuleSO> _allTiles;
        private GenerationSettingsSO _settings;
        private FastRandom _random;
        private Dictionary<TileRuleSO, Dictionary<Direction, HashSet<TileRuleSO>>> _neighborRulesCache;
        private Dictionary<Vector2Int, GameObject> _result;

        public WaveFunctionCollapseAlgorithm(GenerationSettingsSO settings, int seed = 0)
        {
            _settings = settings;
            _allTiles = settings._availableTiles;
            _random = seed == 0 ? new FastRandom() : new FastRandom(seed);
            _result = new Dictionary<Vector2Int, GameObject>();
            _neighborRulesCache = new Dictionary<TileRuleSO, Dictionary<Direction, HashSet<TileRuleSO>>>();
            _cells = new Dictionary<Vector2Int, Cell>();
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
                    directionRules[direction] = rule != null ? new HashSet<TileRuleSO>(rule.allowedNeighbors) : new HashSet<TileRuleSO>(_allTiles);
                }
                _neighborRulesCache[tile] = directionRules;
            }
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

        private async UniTask<bool> Step()
        {
            var uncollapsedCells = _cells.Values.Where(c => !c.IsCollapsed).ToList();
            if (uncollapsedCells.Count == 0) return false;
            
            Cell cellToCollapse = uncollapsedCells[_random.Next(uncollapsedCells.Count)];
            cellToCollapse.Collapse(cellToCollapse.SelectRandomTileWithWeights(_random));
            return await PropagateConstraints(cellToCollapse.Position);
        }
        private static readonly Direction[] directions = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
        private async UniTask<bool> PropagateConstraints(Vector2Int startPos)
        {
            Queue<Vector2Int> propagationQueue = new Queue<Vector2Int>();
            propagationQueue.Enqueue(startPos);
            HashSet<Vector2Int> processedPositions = new HashSet<Vector2Int>();
            
            int iterations = 0;
            while (propagationQueue.Count > 0 && iterations < _settings.maxPropagationIterations)
            {
                iterations++;
                Vector2Int currentPos = propagationQueue.Dequeue();

                if (!_cells.TryGetValue(currentPos, out var currentCell)) continue;
                processedPositions.Add(currentPos);

                Shuffle(directions);

                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int neighborPos = GetNeighborPosition(currentPos, directions[i]);
                    if (neighborPos == currentPos) continue;
                    bool isNewCell = !_cells.ContainsKey(neighborPos);
                    if (isNewCell) _cells[neighborPos] = new Cell(neighborPos, _allTiles);
                    var neighborCell = _cells[neighborPos];
                    bool changed = neighborCell.ConstrainPossibilities(GetAllowedTilesForDirection(currentCell, directions[i]));
                    if (neighborCell.PossibleTiles.Count == 0) return false;
                    if (isNewCell || (changed && !processedPositions.Contains(neighborPos)))
                    {
                        propagationQueue.Enqueue(neighborPos);
                    }
                    await UniTask.Delay(_settings.generationDelay);
                }
            }
            return true;
        }

        private void Shuffle(Direction[] span)
        {
            for (int i = span.Length - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (span[i], span[j]) = (span[j], span[i]);
            }
        }

        private async UniTask RunAlgorithm(CancellationToken cancellationToken)
        {
            _cells.Clear();
            _result.Clear();
            for (int attempts = 0; attempts < _settings.maxGenerationAttempts; attempts++)
            {
                Vector2Int startPos = new Vector2Int(_random.Next(_settings.gridSize.x), _random.Next(_settings.gridSize.y));
                TileRuleSO initialTile = _allTiles[_random.Next(_allTiles.Count)];
                _cells[startPos] = new Cell(startPos, _allTiles);
                _cells[startPos].Collapse(initialTile);
                bool success =  await PropagateConstraints(startPos);
                int stepCount = 0;
                while (success && stepCount < _settings.maxGeneratedCells)
                {
                    stepCount++;
                    await UniTask.Delay(_settings.generationDelay, cancellationToken: cancellationToken);
                    success = await Step();
                    if (cancellationToken.IsCancellationRequested) return;
                }
            }
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

        public async UniTask<Dictionary<Vector2Int, GameObject>> Generate(CancellationToken cancellationToken)
        {
            await RunAlgorithm(cancellationToken);
            CreateResultObjects();
            return _result;
        }
    }
}