using System;
using System.Collections.Generic;
using Unity.Mathematics;
using TheRavine.Base;
using TheRavine.Extensions;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed partial class CellBiosphere : IFoodReceiver
    {
        public const int Morphogens = 6;
        public const int Inputs = Morphogens + 5;
        public const int Genes = 10;
        public const int TargetDivide = Morphogens;
        public const int TargetDie = Morphogens + 1;
        public const int TargetLeaf = Morphogens + 2;
        public const int TargetRoot = Morphogens + 3;
        public const int TargetFruit = Morphogens + 4;
        public const int TargetDigest = Morphogens + 5;
        public const int TargetSpore = Morphogens + 6;
        public const int TargetCount = Morphogens + 7;

        private readonly BiosphereRules _rules;
        private readonly ChemistryTable _table;
        private readonly ChemicalField  _field;
        private readonly EnergyLedger   _ledger;
        private readonly int _substances;
        private readonly int _reactions;
        private readonly int4 _elementSubstance;
        private ChunkFoodIndex _food;
        private DayCycle _day;

        private readonly Dictionary<long, int> _occupancy;
        private readonly long[]  _cellKey;
        private readonly int[]   _cellOrg;
        private readonly byte[]  _cellType;
        private readonly float[] _cellAge;
        private readonly float[] _cellLast;
        private readonly float[] _cellStructure;
        private readonly float[] _morph;
        private int _cellCount;
        private int _cursor;

        private readonly bool[]  _orgAlive;
        private readonly float[] _orgEnergy;
        private readonly float[] _orgPool;
        private readonly float[] _orgStructure;
        private readonly int[]   _orgCells;
        private readonly long[]  _orgAnchor;
        private readonly byte[]  _orgSpore;
        private readonly byte[]  _orgStarving;
        private readonly int[]   _orgGeneration;
        private readonly float[] _geneW;
        private readonly byte[]  _geneT;
        private readonly float[] _activity;
        private readonly int[]   _free;
        private int _freeCount;
        private int _orgCount;

        private float _lastStep = float.NegativeInfinity;

        public int CellCount => _cellCount;
        public int OrganismCount => _orgCount;
        public int MaxCells => _cellKey.Length;
        public int MaxOrganisms => _orgAlive.Length;
        public ChemistryTable Table => _table;
        public ChemicalField Field => _field;
        public BiosphereRules Rules => _rules;
        public long Births { get; private set; }
        public long Deaths { get; private set; }
        public long Spores { get; private set; }
        public long Fruits { get; private set; }
        public long Divisions { get; private set; }

        public CellBiosphere(BiosphereRules rules, EnergyLedger ledger)
        {
            _rules  = rules;
            _ledger = ledger ?? new EnergyLedger();
            _table  = new ChemistryTable(rules.substances, rules.reactions);
            _substances = _table.SubstanceCount;
            _reactions  = _table.ReactionCount;
            _elementSubstance = new int4(_table.ElementSubstance(0), _table.ElementSubstance(1),
                _table.ElementSubstance(2), _table.ElementSubstance(3));

            int cells = math.max(rules.maxCells, 16);
            int orgs  = math.max(rules.maxOrganisms, 1);
            _field = new ChemicalField(cells * 2);

            _occupancy     = new Dictionary<long, int>(cells);
            _cellKey       = new long[cells];
            _cellOrg       = new int[cells];
            _cellType      = new byte[cells];
            _cellAge       = new float[cells];
            _cellLast      = new float[cells];
            _cellStructure = new float[cells];
            _morph         = new float[cells * Morphogens];

            _orgAlive      = new bool[orgs];
            _orgEnergy     = new float[orgs];
            _orgPool       = new float[orgs * math.max(_substances, 1)];
            _orgStructure  = new float[orgs];
            _orgCells      = new int[orgs];
            _orgAnchor     = new long[orgs];
            _orgSpore      = new byte[orgs];
            _orgStarving   = new byte[orgs];
            _orgGeneration = new int[orgs];
            _geneW         = new float[orgs * Genes * Inputs];
            _geneT         = new byte[orgs * Genes];
            _activity      = new float[orgs * math.max(_reactions, 1)];
            _free          = new int[orgs];
            for (int i = orgs - 1; i >= 0; i--) _free[_freeCount++] = i;
        }

        public void BindFood(ChunkFoodIndex food) => _food = food;

        public bool IsAlive(int org) => (uint)org < (uint)_orgAlive.Length && _orgAlive[org];

        public float EnergyOf(int org) => IsAlive(org) ? _orgEnergy[org] : 0f;

        public int CellsOf(int org) => IsAlive(org) ? _orgCells[org] : 0;

        public int GenerationOf(int org) => IsAlive(org) ? _orgGeneration[org] : 0;

        public Span<float> PoolOf(int org) => new Span<float>(_orgPool, org * _substances, _substances);

        public Span<float> ActivityOf(int org) => new Span<float>(_activity, org * _reactions, _reactions);

        public bool TryGetCell(int index, out long key, out CellType type, out int org)
        {
            if ((uint)index >= (uint)_cellCount) { key = 0; type = CellType.Stem; org = -1; return false; }
            key  = _cellKey[index];
            type = (CellType)_cellType[index];
            org  = _cellOrg[index];
            return true;
        }

        public bool IsOccupied(long key) => _occupancy.ContainsKey(key);

        public float TotalEnergy()
        {
            double sum = 0d;
            float structureEnergy = _table.Energy(_rules.structureSubstance);
            for (int o = 0; o < _orgAlive.Length; o++)
            {
                if (!_orgAlive[o]) continue;
                sum += _orgEnergy[o] + _table.BondEnergy(PoolOf(o)) + _orgStructure[o] * structureEnergy;
            }
            return (float)sum;
        }

        public float4 TotalElements()
        {
            float4 sum = _field.Total();
            int4 structure = _table.Composition(_rules.structureSubstance);
            for (int o = 0; o < _orgAlive.Length; o++)
            {
                if (!_orgAlive[o]) continue;
                sum += _table.ElementsOf(PoolOf(o)) + (float4)structure * _orgStructure[o];
            }
            return sum;
        }

        public void Step(float now, ReadOnlySpan<float2> anchors)
        {
            if (!_rules.simulationEnabled) return;
            if (float.IsNegativeInfinity(_lastStep)) _lastStep = now;
            float dt = now - _lastStep;
            if (dt < _rules.stepInterval) return;
            _lastStep = now;
            dt = math.min(dt, _rules.stepInterval * 4f);

            float daylight = Daylight(now);
            _field.Step(dt, _rules);

            int budget = math.min(_rules.cellsPerStep, _cellCount);
            for (int processed = 0; processed < budget && _cellCount > 0; processed++)
            {
                if (_cursor >= _cellCount) _cursor = 0;
                int i = _cursor;
                float cellDt = math.min(now - _cellLast[i], _rules.stepInterval * 4f);
                _cellLast[i] = now;
                if (!UpdateCell(i, math.max(cellDt, 0f), daylight, now)) _cursor++;
            }

            for (int o = 0; o < _orgAlive.Length; o++)
                if (_orgAlive[o]) Metabolize(o, dt, now);

            Seed(now, anchors);
        }

        public float Daylight(float now)
        {
            ref readonly var frame = ref SimulationRules.Frame;
            float light = CreatureCore.IsNightPhase(CreatureCore.DayPhase(ref _day), in frame) ? _rules.nightLight : 1f;
            return SeasonClock.IsWinter(now) ? light * _rules.winterLight : light;
        }

        private void Seed(float now, ReadOnlySpan<float2> anchors)
        {
            if (_orgCount >= _rules.minOrganisms || anchors.Length == 0 || _freeCount == 0 || _cellCount >= _cellKey.Length) return;
            float2 anchor = anchors[RavineRandom.RangeInt(0, anchors.Length)];
            float2 at = anchor + RavineRandom.RangeFloat(0f, _rules.seedRadius) * Direction();
            long key = ChunkFoodIndex.CellOf(at.x, at.y);
            if (_occupancy.ContainsKey(key)) return;
            int o = CreateOrganism(-1, key, now);
            if (o < 0) return;
            _orgEnergy[o] = _rules.seedEnergy;
            _ledger.Flows.BiosphereSeed += _rules.seedEnergy;
        }

        public int SeedAt(long key, float energy, float now)
        {
            if (_occupancy.ContainsKey(key)) return -1;
            int o = CreateOrganism(-1, key, now);
            if (o < 0) return -1;
            _orgEnergy[o] = math.max(energy, 0f);
            _ledger.Flows.BiosphereSeed += _orgEnergy[o];
            return o;
        }

        private static float2 Direction()
        {
            float a = RavineRandom.RangeFloat(0f, 2f * math.PI);
            return new float2(math.cos(a), math.sin(a));
        }

        private int CreateOrganism(int parent, long key, float now)
        {
            if (_freeCount == 0 || _cellCount >= _cellKey.Length) return -1;
            int o = _free[--_freeCount];
            _orgAlive[o] = true;
            _orgEnergy[o] = 0f;
            _orgStructure[o] = 0f;
            _orgCells[o] = 0;
            _orgAnchor[o] = key;
            _orgSpore[o] = 0;
            _orgStarving[o] = 0;
            PoolOf(o).Clear();
            if (parent >= 0)
            {
                InheritGenome(parent, o);
                _orgGeneration[o] = _orgGeneration[parent] + 1;
            }
            else
            {
                RandomGenome(o);
                _orgGeneration[o] = 0;
            }
            _orgCount++;
            Births++;
            AddCell(o, key, now, null);
            return o;
        }

        private int AddCell(int org, long key, float now, int? copyFrom)
        {
            int i = _cellCount++;
            _occupancy[key] = i;
            _cellKey[i] = key;
            _cellOrg[i] = org;
            _cellType[i] = (byte)CellType.Stem;
            _cellAge[i] = 0f;
            _cellLast[i] = now;
            _cellStructure[i] = 0f;
            for (int k = 0; k < Morphogens; k++)
                _morph[i * Morphogens + k] = copyFrom.HasValue ? _morph[copyFrom.Value * Morphogens + k] : 0f;
            _orgCells[org]++;
            return i;
        }

        private void RemoveCell(int i)
        {
            int o = _cellOrg[i];
            long key = _cellKey[i];
            if (_orgAlive[o])
            {
                PoolOf(o)[_rules.structureSubstance] += _cellStructure[i];
                _orgStructure[o] = math.max(_orgStructure[o] - _cellStructure[i], 0f);
                _orgCells[o]--;
                _orgAnchor[o] = key;
            }
            _occupancy.Remove(key);

            int last = --_cellCount;
            if (i != last)
            {
                _cellKey[i] = _cellKey[last];
                _cellOrg[i] = _cellOrg[last];
                _cellType[i] = _cellType[last];
                _cellAge[i] = _cellAge[last];
                _cellLast[i] = _cellLast[last];
                _cellStructure[i] = _cellStructure[last];
                for (int k = 0; k < Morphogens; k++) _morph[i * Morphogens + k] = _morph[last * Morphogens + k];
                _occupancy[_cellKey[i]] = i;
            }

            if (_orgAlive[o] && _orgCells[o] <= 0) KillOrganism(o);
        }

        private void KillOrganism(int o)
        {
            var pool = PoolOf(o);
            pool[_rules.structureSubstance] += _orgStructure[o];
            _orgStructure[o] = 0f;
            float energy = math.max(_orgEnergy[o], 0f) + _table.BondEnergy(pool);
            float4 elements = _table.ElementsOf(pool);
            pool.Clear();
            _orgEnergy[o] = 0f;

            long key = _orgAnchor[o];
            _field.Add(key, elements, _rules);
            float2 at = ChunkFoodIndex.CellCenter(key);
            float stored = _food != null ? _food.ReturnToSoil(at.x, at.y, energy) : 0f;
            ref var flows = ref _ledger.Flows;
            flows.SoilReturn += stored;
            flows.BiosphereUpkeep += energy - stored;

            _orgAlive[o] = false;
            _orgCells[o] = 0;
            _free[_freeCount++] = o;
            _orgCount--;
            Deaths++;
        }

        public void ReceiveFood(float energy)
        {
            if (energy > 0f) _ledger.Flows.BiosphereUpkeep += energy;
        }
    }
}
