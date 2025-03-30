using System.Collections.Generic;
using System;
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
        
        public Cell(Vector2Int position, TileRuleSO tileRuleSO)
        {
            Position = position;
            CollapsedTile = tileRuleSO;
            PossibleTiles = new List<TileRuleSO>();
            PossibleTiles.Add(tileRuleSO);
        }

        public void Collapse(TileRuleSO tile)
        {
            CollapsedTile = tile;
            PossibleTiles.Clear();
            PossibleTiles.Add(tile);
        }
        public bool ConstrainPossibilities(HashSet<TileRuleSO> allowedTiles)
        {
            if (IsCollapsed) return false;

            int initialCount = PossibleTiles.Count;
            PossibleTiles.RemoveAll(tile => !allowedTiles.Contains(tile));

            return PossibleTiles.Count != initialCount;
        }

        public TileRuleSO SelectRandomTileWithWeights(FastRandom random)
        {
            if (PossibleTiles.Count == 0) return null;
            if (PossibleTiles.Count == 1) return PossibleTiles[0];
            int[] prefixSums = new int[PossibleTiles.Count];
            prefixSums[0] = PossibleTiles[0].weight;

            for (int i = 1; i < PossibleTiles.Count; i++)
            {
                prefixSums[i] = prefixSums[i - 1] + PossibleTiles[i].weight;
            }

            int totalWeight = prefixSums[PossibleTiles.Count - 1];
            if (totalWeight <= 0) return PossibleTiles[random.Next(PossibleTiles.Count)];

            int randomWeight = random.Next(totalWeight);

            int left = 0, right = PossibleTiles.Count - 1;
            while (left < right)
            {
                int mid = (left + right) / 2;
                if (prefixSums[mid] <= randomWeight)
                    left = mid + 1;
                else
                    right = mid;
            }

            return PossibleTiles[left];
        }
    }
    public class WaveFunctionCollapseAlgorithm
    {
        private readonly Dictionary<Vector2Int, Cell> cells;
        private readonly List<TileRuleSO> allTiles;
        private readonly GenerationSettingsSO settings;
        private readonly FastRandom random;
        private readonly Dictionary<(TileRuleSO, Direction), HashSet<TileRuleSO>> neighborRulesCache;
        private readonly Dictionary<Vector2Int, GameObject> result;
        private static readonly Direction[] directions = { Direction.Up, Direction.Right, Direction.Down, Direction.Left };
        public WaveFunctionCollapseAlgorithm(GenerationSettingsSO settings, int seed = 0)
        {
            this.settings = settings;
            allTiles = settings._availableTiles;
            random = seed == 0 ? new FastRandom() : new FastRandom(seed);
            result = new Dictionary<Vector2Int, GameObject>();
            neighborRulesCache = new Dictionary<(TileRuleSO, Direction), HashSet<TileRuleSO>>();
            cells = new Dictionary<Vector2Int, Cell>();
            InitializeNeighborRulesCache();
        }

        private readonly HashSet<TileRuleSO> reusableAllowedTilesSet = new HashSet<TileRuleSO>();
        private void InitializeNeighborRulesCache()
        {
            foreach (var tile in allTiles)
            {
                foreach (var rule in tile.rules)
                {
                    neighborRulesCache[(tile, rule.direction)] = new HashSet<TileRuleSO>(rule.allowedNeighbors);
                }
            }
        }
        private HashSet<TileRuleSO> GetAllowedTilesForDirection(Cell cell, Direction direction)
        {
            reusableAllowedTilesSet.Clear();
            foreach (var possibleTile in cell.PossibleTiles)
            {
                if (neighborRulesCache.TryGetValue((possibleTile, direction), out var allowedNeighbors))
                {
                    reusableAllowedTilesSet.UnionWith(allowedNeighbors);
                }
            }
            return reusableAllowedTilesSet;
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

            if (settings.borderRule == GenerationSettingsSO.BorderRuleType.Wrap)
            {
                neighborPos.x = (neighborPos.x + settings.gridSize.x) % settings.gridSize.x;
                neighborPos.y = (neighborPos.y + settings.gridSize.y) % settings.gridSize.y;
            }
            else if (settings.borderRule == GenerationSettingsSO.BorderRuleType.Block)
            {
                if (neighborPos.x < 0 || neighborPos.x >= settings.gridSize.x ||
                    neighborPos.y < 0 || neighborPos.y >= settings.gridSize.y)
                {
                    return position;
                }
            }

            return neighborPos;
        }
        private readonly IndexedSet<Cell> uncollapsedCells = new IndexedSet<Cell>();
        private async UniTask<bool> Step()
        {
            if (uncollapsedCells.Count == 0) return false;

            Cell cellToCollapse = uncollapsedCells.GetRandom(random);
            cellToCollapse.Collapse(cellToCollapse.SelectRandomTileWithWeights(random));
            uncollapsedCells.Remove(cellToCollapse);
            return await PropagateConstraints(cellToCollapse.Position);
        }
        private readonly HashSet<Vector2Int> processedPositions = new HashSet<Vector2Int>();
        private readonly Queue<Vector2Int> propagationQueue = new Queue<Vector2Int>();
        private async UniTask<bool> PropagateConstraints(Vector2Int startPos)
        {
            propagationQueue.Clear();
            processedPositions.Clear();
            propagationQueue.Enqueue(startPos);
            Shuffle(directions);

            int iterations = 0;
            int batchSize = 5; 
            while (propagationQueue.Count > 0 && iterations < settings.maxPropagationIterations)
            {
                iterations++;
                Vector2Int currentPos = propagationQueue.Dequeue();
                if (!cells.TryGetValue(currentPos, out var currentCell)) continue;
                processedPositions.Add(currentPos);
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector2Int neighborPos = GetNeighborPosition(currentPos, directions[i]);
                    if (neighborPos == currentPos) continue;
                    bool isNewCell = !cells.ContainsKey(neighborPos);
                    if (isNewCell)
                    {
                        if(cells.Count <= settings.maxGeneratedCells)
                        {
                            Cell newCell = new Cell(neighborPos, allTiles);
                            cells[neighborPos] = newCell;
                            uncollapsedCells.Add(newCell);
                        }
                        else
                            break;
                    }   
                    Cell neighborCell = cells[neighborPos];
                    bool changed = neighborCell.ConstrainPossibilities(GetAllowedTilesForDirection(currentCell, directions[i]));
                    if (neighborCell.PossibleTiles.Count == 1 && !neighborCell.IsCollapsed)
                    {
                        neighborCell.Collapse(neighborCell.PossibleTiles[0]);
                        uncollapsedCells.Remove(neighborCell);
                    }
                    if (neighborCell.PossibleTiles.Count == 0) return false;
                    if (isNewCell || (changed && !processedPositions.Contains(neighborPos)))
                    {
                        propagationQueue.Enqueue(neighborPos);
                    }
                }

                if (iterations % batchSize == 0)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            return true;
        }
        private void Shuffle(Direction[] span)
        {
            for (int i = span.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (span[i], span[j]) = (span[j], span[i]);
            }
        }
        private void ApplyInitialPattern(Vector2Int origin, TilePatternSO initialPattern)
        {
            foreach (var pattern in initialPattern.initialPatterns)
            {
                Vector2Int position = origin + pattern.startOffset;
                
                if (settings.borderRule == GenerationSettingsSO.BorderRuleType.Block)
                {
                    if (position.x < 0 || position.x >= settings.gridSize.x ||
                        position.y < 0 || position.y >= settings.gridSize.y)
                    {
                        continue;
                    }
                }
                cells[position] = new Cell(position, pattern.tile);
                propagationQueue.Enqueue(position);
            }
        }
        private void CreateResultObjects()
        {
            foreach (var cell in cells.Values)
            {
                if (!cell.IsCollapsed && settings.IsGreedyCollapse)
                {
                    cell.Collapse(cell.SelectRandomTileWithWeights(random));
                    uncollapsedCells.Remove(cell);
                }

                if(cell.IsCollapsed)
                    result[cell.Position] = cell.CollapsedTile.prefab;
            }
        }

        public async UniTask<Dictionary<Vector2Int, GameObject>> Generate(CancellationToken cancellationToken, Vector2Int triggerPosition, TileRuleSO _initialTile, TilePatternSO initialPattern)
        {
            cells.Clear();
            result.Clear();
            uncollapsedCells.Clear();

            Vector2Int startPos = triggerPosition != null ? triggerPosition : new Vector2Int(random.Next(settings.gridSize.x), random.Next(settings.gridSize.y));
            TileRuleSO initialTile = _initialTile != null ? _initialTile : allTiles[random.Next(allTiles.Count)];
            
            if (initialPattern == null || initialPattern.initialPatterns == null)
                cells[startPos] = new Cell(startPos, initialTile);
            else
                ApplyInitialPattern(startPos, initialPattern);

            bool success =  await PropagateConstraints(startPos);
            int stepCount = 0;

            for (int attempts = 0; attempts < settings.maxGenerationAttempts; attempts++)
            {
                stepCount = 0;
                while (success && stepCount < settings.maxStepIterations)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    stepCount++;
                    await UniTask.Delay(settings.generationDelay, cancellationToken: cancellationToken);
                    success = await Step();
                }
            }

            CreateResultObjects();
            return result;
        }
    }
}