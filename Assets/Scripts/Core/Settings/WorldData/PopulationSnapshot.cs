using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheRavine.Base
{
    public sealed class PopulationSnapshot
    {
        public const int Version = 4;
        public const int MaxHelpEntries = 32;
        public const int MinVersion = 1;
        public const int GeneCount = 53;
        public const int MaxAgents = 65536;
        public const int MaxColonies = 256;

        public struct Colony
        {
            public int Id, Leader;
            public bool Wild;
            public byte Arm;
            public float X, Z, Storage, Hunger, LearningRate, GradientNorm, Entropy;
            public double ElectionRemaining;
        }

        public struct HelpRecord
        {
            public int Partner;
            public float Balance, ComfortAccum;
            public float BalanceAge, ComfortAge, GaveAge, ReceivedAge, MetAge;
        }

        public struct Agent
        {
            public int Id, ColonyId, Parent, Mate;
            public uint MateLoci;
            public Vector3 Position;
            public float Age, Health, Energy, Stomach, Carrying;
            public double AttackRemaining, ReviewRemaining;
            public byte Caste;
            public bool Fixed;
            public float Stress, LonelySeconds;
            public byte StressLevel;
            public HelpRecord[] Help;
            public float[] Genes;
        }

        public int Seed, LastEntityId, LastColonyId;
        public long WorldCreated, Captured;
        public Colony[] Colonies;
        public Agent[] Agents;

        internal static void Require(bool valid, string reason)
        {
            if (!valid) throw new InvalidDataException("Population: " + reason);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool WorldCoordinate(float value) => value > int.MinValue && value < int.MaxValue;

        private static bool Duration(double value) => value >= 0 && !double.IsInfinity(value);

        private static bool PositiveOrZero(float value) => value >= 0 && Finite(value);

        private static bool AgeOrNever(float value) => (value >= 0 || value == -1) && Finite(value);

        public void Validate(int seed, long created)
        {
            Require(Seed == seed && WorldCreated == created, "world binding mismatch");
            Require(Captured >= 0 && LastEntityId >= 0 && LastEntityId < 0x3fffffff &&
                LastColonyId >= 0 && LastColonyId < 0x3fffffff, "invalid header");
            Require(Colonies != null && Colonies.Length <= MaxColonies && Agents != null && Agents.Length <= MaxAgents, "invalid counts");
            var colonies = new HashSet<int>();
            var agents = new Dictionary<int, int>();
            bool hasWild = false;
            foreach (var c in Colonies)
            {
                Require(c.Id > 0 && c.Id <= LastColonyId && colonies.Add(c.Id), "invalid or duplicate colony ID");
                Require(c.Leader >= 0 && c.Leader <= LastEntityId && c.Arm < 7 && !(hasWild && c.Wild), "invalid colony");
                hasWild |= c.Wild;
                Require(WorldCoordinate(c.X) && WorldCoordinate(c.Z) && PositiveOrZero(c.Storage) && c.Hunger >= 0 && c.Hunger <= 1 && Duration(c.ElectionRemaining), "invalid nest");
                Require(PositiveOrZero(c.LearningRate) && PositiveOrZero(c.GradientNorm) && PositiveOrZero(c.Entropy), "invalid colony genome");
            }
            foreach (var a in Agents)
            {
                Require(a.Id > 0 && a.Id <= LastEntityId && agents.TryAdd(a.Id, a.ColonyId), "invalid or duplicate agent ID");
                Require(colonies.Contains(a.ColonyId) && a.Parent >= 0 && a.Parent < a.Id && a.Mate >= 0 && a.Mate < a.Id, "invalid colony or parent reference");
                Require(a.Caste < 4 && WorldCoordinate(a.Position.x) && WorldCoordinate(a.Position.y) && WorldCoordinate(a.Position.z), "invalid caste or position");
                Require(PositiveOrZero(a.Age) && a.Health > 0 && Finite(a.Health) && PositiveOrZero(a.Energy) &&
                    PositiveOrZero(a.Stomach) && PositiveOrZero(a.Carrying) && Duration(a.AttackRemaining) && Duration(a.ReviewRemaining), "invalid vitals");
                Require(Finite(a.Stress) && a.Stress >= 0 && a.Stress <= 1 && PositiveOrZero(a.LonelySeconds) && a.StressLevel <= 2, "invalid stress state");
                Require(a.Help == null || a.Help.Length <= MaxHelpEntries, "too many help entries");
                if (a.Help != null)
                    foreach (var h in a.Help)
                        Require(h.Partner != 0 && Finite(h.Balance) && PositiveOrZero(h.ComfortAccum) && AgeOrNever(h.BalanceAge) && AgeOrNever(h.ComfortAge) &&
                            AgeOrNever(h.GaveAge) && AgeOrNever(h.ReceivedAge) && AgeOrNever(h.MetAge), "invalid help entry");
                Require(a.Genes != null && a.Genes.Length == GeneCount, "unexpected phenotype shape");
                foreach (float gene in a.Genes) Require(Finite(gene), "non-finite phenotype");
            }
            foreach (var c in Colonies)
                Require(!agents.TryGetValue(c.Leader, out int home) || home == c.Id, "leader belongs to another colony");
        }
    }
}
