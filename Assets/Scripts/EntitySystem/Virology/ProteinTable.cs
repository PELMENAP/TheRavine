using System;
using System.Runtime.CompilerServices;

namespace TheRavine.EntityControl.Virology
{
    public enum ProteinAction : byte
    {
        Noop,
        AddHealth,
        DrainHealth,
        AddEnergy,
        DrainEnergy,
        RegenBoost,
        MetabolismUp,
        MetabolismDown,
        ModifyWander,
        ModifyForage,
        ModifyHunt,
        ModifySocial,
        ModifyFlee,
        SharpnessUp,
        SharpnessDown,
        Prime,
        Replicate,
        SpreadToOther,
        Reinforce,
        Excise,
        Dormant,
        MutationRateUp,
        MutationRateDown,
        Junk
    }

    public readonly struct ProteinDescriptor
    {
        public readonly ProteinAction Action;
        public readonly float BaseMagnitude;
        public readonly float EnergyCost;
        public readonly bool RequiresPrime;
        public readonly bool IsViralIntent;

        public ProteinDescriptor(ProteinAction action, float baseMagnitude, float energyCost,
            bool requiresPrime, bool isViralIntent)
        {
            Action = action;
            BaseMagnitude = baseMagnitude;
            EnergyCost = energyCost;
            RequiresPrime = requiresPrime;
            IsViralIntent = isViralIntent;
        }
    }

    public static class ProteinTable
    {
        public const int ActionCount = 24;
        public const int EmbedDim = 8;

        public const float SpreadCostFloor = 0.2f;
        public const float SpreadCostGain = 4.5f;

        public static readonly ProteinDescriptor[] Descriptors =
        {
            new(ProteinAction.Noop,            0f,    0f,    false, false),
            new(ProteinAction.AddHealth,       0.90f, 0.35f, false, false),
            new(ProteinAction.DrainHealth,     1.20f, 0.05f, false, true),
            new(ProteinAction.AddEnergy,       1.10f, 0.05f, false, false),
            new(ProteinAction.DrainEnergy,     1.60f, 0.02f, false, true),
            new(ProteinAction.RegenBoost,      0.50f, 0.20f, false, false),
            new(ProteinAction.MetabolismUp,    0.35f, 0.15f, false, false),
            new(ProteinAction.MetabolismDown,  0.35f, 0.15f, false, false),
            new(ProteinAction.ModifyWander,    0.40f, 0.10f, false, false),
            new(ProteinAction.ModifyForage,    0.40f, 0.10f, false, false),
            new(ProteinAction.ModifyHunt,      0.40f, 0.12f, false, true),
            new(ProteinAction.ModifySocial,    0.40f, 0.10f, false, false),
            new(ProteinAction.ModifyFlee,      0.40f, 0.10f, false, false),
            new(ProteinAction.SharpnessUp,     0.05f, 0.25f, false, false),
            new(ProteinAction.SharpnessDown,   0.05f, 0.25f, false, false),
            new(ProteinAction.Prime,           0f,    0.30f, false, true),
            new(ProteinAction.Replicate,       1f,    2.50f, true,  true),
            new(ProteinAction.SpreadToOther,   1f,    1.20f, true,  true),
            new(ProteinAction.Reinforce,       0.25f, 0.60f, false, true),
            new(ProteinAction.Excise,          1f,    3.00f, true,  false),
            new(ProteinAction.Dormant,         0f,    0f,    false, true),
            new(ProteinAction.MutationRateUp,  0.15f, 0.10f, false, true),
            new(ProteinAction.MutationRateDown,0.15f, 0.10f, false, true),
            new(ProteinAction.Junk,            0f,    0f,    false, false),
        };

        public static readonly float[] CellBias =
        {
            0.10f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f, 0f,
            -0.05f, -0.05f,
            -0.10f, -0.20f, -0.20f, -0.05f, -0.15f,
            0.05f, -0.05f, -0.05f,
            0.55f
        };

        static ProteinTable()
        {
            int enumCount = Enum.GetValues(typeof(ProteinAction)).Length;
            if (enumCount != ActionCount || Descriptors.Length != ActionCount || CellBias.Length != ActionCount)
                throw new InvalidOperationException(
                    $"ProteinTable: enum({enumCount}) desc({Descriptors.Length}) bias({CellBias.Length}) != {ActionCount}");

            for (int i = 0; i < ActionCount; i++)
                if ((int)Descriptors[i].Action != i)
                    throw new InvalidOperationException($"ProteinTable: descriptor order broken at {i}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveEnergyCost(int action, float baseCost, float amp)
            => action == (int)ProteinAction.SpreadToOther
                ? baseCost * (SpreadCostFloor + amp * amp * SpreadCostGain)
                : baseCost;
    }
}