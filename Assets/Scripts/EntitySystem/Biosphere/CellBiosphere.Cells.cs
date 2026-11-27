using System;
using Unity.Mathematics;
using TheRavine.Extensions;

namespace TheRavine.EntityControl.Biosphere
{
    public sealed partial class CellBiosphere
    {
        private bool UpdateCell(int i, float dt, float daylight, float now)
        {
            int o = _cellOrg[i];
            long key = _cellKey[i];
            _orgAnchor[o] = key;

            if (_orgStarving[o] != 0)
            {
                _orgStarving[o] = 0;
                RemoveCell(i);
                return true;
            }

            int x = Position2Int.GetX(key), z = Position2Int.GetY(key);
            Span<float> avg = stackalloc float[Morphogens];
            int neighbours = 0;
            neighbours += Neighbour(Position2Int.Pack(x + 1, z), o, avg);
            neighbours += Neighbour(Position2Int.Pack(x - 1, z), o, avg);
            neighbours += Neighbour(Position2Int.Pack(x, z + 1), o, avg);
            neighbours += Neighbour(Position2Int.Pack(x, z - 1), o, avg);

            Span<float> input = stackalloc float[Inputs];
            int m0 = i * Morphogens;
            for (int k = 0; k < Morphogens; k++) input[k] = _morph[m0 + k];
            input[Morphogens]     = neighbours * 0.25f;
            input[Morphogens + 1] = math.saturate(_cellAge[i] / _rules.ageNorm);
            input[Morphogens + 2] = math.saturate(_orgEnergy[o] / (_rules.energyNorm * math.max(_orgCells[o], 1)));
            input[Morphogens + 3] = Resource(key);
            input[Morphogens + 4] = 1f;

            Span<float> produce = stackalloc float[Morphogens];
            int actions = 0;
            int gw = o * Genes * Inputs;
            for (int g = 0; g < Genes; g++)
            {
                float a = 0f;
                for (int k = 0; k < Inputs; k++) a += _geneW[gw + g * Inputs + k] * input[k];
                if (!(a > 0f)) continue;
                int t = _geneT[o * Genes + g];
                if (t < Morphogens) produce[t] += math.min(a, 1f);
                else actions |= 1 << (t - Morphogens);
            }

            float decay = _rules.morphogenDecay, diffusion = _rules.morphogenDiffusion, production = _rules.morphogenProduction;
            for (int k = 0; k < Morphogens; k++)
            {
                float m = _morph[m0 + k];
                float spread = neighbours > 0 ? diffusion * (avg[k] / neighbours - m) : 0f;
                _morph[m0 + k] = math.clamp(m + dt * (production * produce[k] - decay * m + spread), 0f, _rules.morphogenMax);
            }
            _cellAge[i] += dt;

            float2 at = ChunkFoodIndex.CellCenter(key);
            ref var flows = ref _ledger.Flows;
            switch ((CellType)_cellType[i])
            {
                case CellType.Leaf:
                {
                    float gain = _rules.lightPerLeaf * daylight * dt;
                    _orgEnergy[o] += gain;
                    flows.BiosphereLight += gain;
                    break;
                }
                case CellType.Root:
                    Absorb(o, key, at, dt);
                    break;
                case CellType.Digest:
                    Digest(o, at, now);
                    break;
                case CellType.Fruit:
                    Ripen(i, o, at);
                    break;
            }

            float upkeep = _rules.cellUpkeep * dt;
            float paid = math.min(upkeep, math.max(_orgEnergy[o], 0f));
            _orgEnergy[o] -= paid;
            flows.BiosphereUpkeep += paid;
            if (paid < upkeep) _orgStarving[o] = 1;

            if ((actions & (1 << (TargetDie - Morphogens))) != 0)
            {
                RemoveCell(i);
                return true;
            }
            if ((actions & (1 << (TargetLeaf - Morphogens))) != 0) _cellType[i] = (byte)CellType.Leaf;
            else if ((actions & (1 << (TargetRoot - Morphogens))) != 0) _cellType[i] = (byte)CellType.Root;
            else if ((actions & (1 << (TargetFruit - Morphogens))) != 0) _cellType[i] = (byte)CellType.Fruit;
            else if ((actions & (1 << (TargetDigest - Morphogens))) != 0) _cellType[i] = (byte)CellType.Digest;
            if ((actions & (1 << (TargetSpore - Morphogens))) != 0) _orgSpore[o] = 1;
            if ((actions & (1 << (TargetDivide - Morphogens))) != 0) TryDivide(i, o, x, z, now);
            return false;
        }

        private int Neighbour(long key, int org, Span<float> avg)
        {
            if (!_occupancy.TryGetValue(key, out int j) || _cellOrg[j] != org) return 0;
            for (int k = 0; k < Morphogens; k++) avg[k] += _morph[j * Morphogens + k];
            return 1;
        }

        private float Resource(long key)
        {
            float4 amount;
            if (!_field.TryGet(key, out amount))
                amount = ChemicalField.Ambient(key, _rules.ambientElement, _rules.ambientVariation, _rules.ambientNoiseScale);
            return math.saturate(math.csum(amount) / math.max(4f * _rules.ambientElement, 1e-3f));
        }

        private void Absorb(int o, long key, float2 at, float dt)
        {
            var pool = PoolOf(o);
            float room = math.max(_rules.poolPerCell * _orgCells[o] - Sum(pool), 0f);
            float want = math.min(_rules.rootElementUptake * dt, room * 0.25f);
            if (want > 0f)
            {
                float4 taken = _field.Take(key, new float4(want), _rules);
                for (int e = 0; e < ChemistryTable.Elements; e++)
                {
                    int s = _elementSubstance[e];
                    if (s >= 0) pool[s] += taken[e];
                    else _field.Add(key, Element(e, taken[e]), _rules);
                }
            }
            if (_food != null && _rules.rootSoilUptake > 0f)
            {
                float soil = _food.TakeSoil(at.x, at.y, _rules.rootSoilUptake * dt);
                _orgEnergy[o] += soil;
                _ledger.Flows.BiosphereSoil += soil;
            }
        }

        private static float4 Element(int e, float amount)
        {
            float4 v = float4.zero;
            v[e] = amount;
            return v;
        }

        private void Digest(int o, float2 at, float now)
        {
            if (_food == null || _rules.digestReach <= 0f) return;
            if (!_food.TryFindNearestFood(at.x, at.y, _rules.digestReach, out long cell, out _)) return;
            if (!_food.TryGetKind(cell, out FoodKind kind, out _) || kind == FoodKind.Large) return;
            if (!_food.TryClaim(cell, this, now, out FoodClaim claim) || claim.Pending || !(claim.Energy > 0f)) return;
            _orgEnergy[o] += claim.Energy;
        }

        private void Ripen(int i, int o, float2 at)
        {
            ref readonly var frame = ref SimulationRules.Frame;
            float expected = frame.EatEnergyFood * frame.FoodMinNutritionFraction;
            if (_food == null || _cellAge[i] < _rules.fruitAge || _orgEnergy[o] < _rules.fruitReserve + expected) return;
            if (!_food.TryAddFood(at, FoodKind.Plant, out float nutrition)) return;
            _orgEnergy[o] -= nutrition;
            if (_orgEnergy[o] < 0f)
            {
                _ledger.Flows.BiosphereSeed += -_orgEnergy[o];
                _orgEnergy[o] = 0f;
            }
            _cellAge[i] = 0f;
            Fruits++;
        }

        private void TryDivide(int i, int o, int x, int z, float now)
        {
            if (_orgCells[o] >= _rules.maxCellsPerOrganism || _cellCount >= _cellKey.Length) return;
            var pool = PoolOf(o);
            int s = _rules.structureSubstance;
            if ((uint)s >= (uint)_substances || pool[s] < _rules.divideStructure || _orgEnergy[o] < _rules.divideEnergy) return;

            long best = 0;
            float bestScore = float.NegativeInfinity;
            for (int d = 0; d < 4; d++)
            {
                long k = d switch
                {
                    0 => Position2Int.Pack(x + 1, z),
                    1 => Position2Int.Pack(x - 1, z),
                    2 => Position2Int.Pack(x, z + 1),
                    _ => Position2Int.Pack(x, z - 1),
                };
                if (_occupancy.ContainsKey(k)) continue;
                float score = Resource(k) + RavineRandom.RangeFloat(0f, _rules.divideJitter);
                if (score > bestScore) { bestScore = score; best = k; }
            }
            if (float.IsNegativeInfinity(bestScore)) return;

            pool[s] -= _rules.divideStructure;
            _orgStructure[o] += _rules.divideStructure;
            _orgEnergy[o] -= _rules.divideEnergy;
            _ledger.Flows.BiosphereUpkeep += _rules.divideEnergy;
            int child = AddCell(o, best, now, i);
            _cellStructure[child] = _rules.divideStructure;
            Divisions++;
        }

        private void Metabolize(int o, float dt, float now)
        {
            var pool = PoolOf(o);
            float energy = _orgEnergy[o];
            ReactionSolver.Step(_table, pool, ref energy, ActivityOf(o), _rules.reactionRate, dt, _rules.catabolicEfficiency, out float loss);
            _orgEnergy[o] = energy;
            _ledger.Flows.BiosphereUpkeep += loss;

            float cap = _rules.poolPerCell * math.max(_orgCells[o], 1);
            float total = Sum(pool);
            if (total > cap)
            {
                float4 excess = float4.zero;
                float share = 1f - cap / total;
                for (int e = 0; e < ChemistryTable.Elements; e++)
                {
                    int s = _elementSubstance[e];
                    if (s < 0) continue;
                    float out_ = pool[s] * share;
                    pool[s] -= out_;
                    excess[e] = out_;
                }
                _field.Add(_orgAnchor[o], excess, _rules);
            }

            if (_orgSpore[o] != 0)
            {
                _orgSpore[o] = 0;
                TrySpore(o, now);
            }
        }

        private void TrySpore(int o, float now)
        {
            if (_orgCells[o] < _rules.sporeMinCells || _orgEnergy[o] < _rules.sporeEnergy * 2f || _freeCount == 0) return;
            float2 at = ChunkFoodIndex.CellCenter(_orgAnchor[o]) + RavineRandom.RangeFloat(1f, math.max(_rules.sporeRange, 1f)) * Direction();
            long key = ChunkFoodIndex.CellOf(at.x, at.y);
            if (_occupancy.ContainsKey(key)) return;
            int child = CreateOrganism(o, key, now);
            if (child < 0) return;
            _orgEnergy[o] -= _rules.sporeEnergy;
            _orgEnergy[child] = _rules.sporeEnergy;
            var from = PoolOf(o);
            var to = PoolOf(child);
            for (int s = 0; s < _substances; s++)
            {
                float give = from[s] * _rules.sporePoolShare;
                from[s] -= give;
                to[s] += give;
            }
            Spores++;
        }

        private static float Sum(ReadOnlySpan<float> pool)
        {
            float sum = 0f;
            for (int i = 0; i < pool.Length; i++) sum += pool[i];
            return sum;
        }
    }
}
