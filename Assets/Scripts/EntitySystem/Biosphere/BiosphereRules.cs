using UnityEngine;
using Unity.Mathematics;

namespace TheRavine.EntityControl.Biosphere
{
    [CreateAssetMenu(fileName = "BiosphereRules", menuName = "Gameplay/Biosphere Rules")]
    public sealed class BiosphereRules : ScriptableObject
    {
        public bool simulationEnabled;
        public float stepInterval = 1f;
        public int maxCells = 8192;
        public int maxOrganisms = 512;
        public int cellsPerStep = 2048;
        public int minOrganisms = 24;
        public float seedRadius = 40f;
        public float seedEnergy = 20f;
        public int maxCellsPerOrganism = 64;

        public SubstanceDef[] substances =
        {
            new("A", new int4(1, 0, 0, 0), 0f),
            new("B", new int4(0, 1, 0, 0), 0f),
            new("C", new int4(0, 0, 1, 0), 0f),
            new("D", new int4(0, 0, 0, 1), 0f),
            new("AB", new int4(1, 1, 0, 0), 2f),
            new("CD", new int4(0, 0, 1, 1), 3f),
            new("ABC", new int4(1, 1, 1, 0), 4f),
            new("ABCD", new int4(1, 1, 1, 1), 6f),
        };

        public ReactionDef[] reactions =
        {
            new(0, 1, 4, -1),
            new(4, -1, 0, 1),
            new(2, 3, 5, -1),
            new(5, -1, 2, 3),
            new(4, 2, 6, -1),
            new(6, -1, 4, 2),
            new(6, 3, 7, -1),
            new(7, -1, 6, 3),
            new(7, -1, 4, 5),
            new(4, 5, 7, -1),
        };

        public int structureSubstance = 7;
        public float reactionRate = 0.5f;
        public float catabolicEfficiency = 0.8f;

        public float ambientElement = 4f;
        public float ambientVariation = 0.75f;
        public float ambientNoiseScale = 0.02f;
        public float weatheringRate = 0.01f;
        public float fieldDiffusion = 0.1f;
        public float fieldMaxPerCell = 50f;

        public float lightPerLeaf = 0.08f;
        public float nightLight = 0.1f;
        public float winterLight = 0.4f;
        public float rootElementUptake = 0.4f;
        public float rootSoilUptake = 0.05f;
        public float digestReach = 1.5f;
        public float cellUpkeep = 0.01f;
        public float poolPerCell = 8f;
        public float divideStructure = 1f;
        public float divideEnergy = 0.5f;
        public float fruitAge = 30f;
        public float fruitReserve = 4f;
        public float sporeEnergy = 6f;
        public float sporeRange = 12f;
        public int sporeMinCells = 4;
        public float energyNorm = 10f;
        public float ageNorm = 120f;

        public float morphogenDecay = 0.2f;
        public float morphogenDiffusion = 0.3f;
        public float morphogenProduction = 1f;
        public float geneMutation = 0.1f;
        public float geneMutationScale = 0.3f;
        public float targetMutation = 0.02f;
        public float activityMutation = 0.1f;
        public float activityMutationScale = 0.2f;
        public float sporePoolShare = 0.25f;
        public float morphogenMax = 4f;
        public float divideJitter = 0.05f;

        private void OnValidate()
        {
            stepInterval = Mathf.Max(stepInterval, 0.05f);
            maxCells = Mathf.Clamp(maxCells, 16, 1 << 18);
            maxOrganisms = Mathf.Clamp(maxOrganisms, 1, 1 << 14);
            cellsPerStep = Mathf.Max(cellsPerStep, 1);
            minOrganisms = Mathf.Clamp(minOrganisms, 0, maxOrganisms);
            maxCellsPerOrganism = Mathf.Max(maxCellsPerOrganism, 1);
            reactionRate = Mathf.Max(reactionRate, 0f);
            catabolicEfficiency = Mathf.Clamp01(catabolicEfficiency);
            fieldDiffusion = Mathf.Clamp(fieldDiffusion, 0f, 0.24f);
            morphogenDiffusion = Mathf.Clamp(morphogenDiffusion, 0f, 1f);
            morphogenDecay = Mathf.Max(morphogenDecay, 0f);
            geneMutation = Mathf.Clamp01(geneMutation);
            targetMutation = Mathf.Clamp01(targetMutation);
            activityMutation = Mathf.Clamp01(activityMutation);
            sporePoolShare = Mathf.Clamp01(sporePoolShare);
            morphogenMax = Mathf.Max(morphogenMax, 0f);
            divideJitter = Mathf.Max(divideJitter, 0f);
            energyNorm = Mathf.Max(energyNorm, 1e-3f);
            ageNorm = Mathf.Max(ageNorm, 1e-3f);
        }
    }
}
