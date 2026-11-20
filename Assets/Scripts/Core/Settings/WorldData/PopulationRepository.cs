using System.IO;
using UnityEngine;
using static TheRavine.Base.PopulationSnapshot;

namespace TheRavine.Base
{
    public static class PopulationRepository
    {
        private const uint Magic = 0x31504F50;
        private const int HeaderBytes = 44;
        private const int ColonyBytes = 46;
        private const int AgentBytes = 62 + sizeof(float) * GeneCount;

        private static string FilePath(string world)
        {
            if (!WorldRegistry.TryGetSafeWorldDirectory(world, out var directory))
                throw new InvalidDataException("Invalid population directory");
            return Path.Combine(directory, "population.bin");
        }

        public static PopulationSnapshot Load(string world, int seed, long created)
        {
            string path = FilePath(world);
            try
            {
                using var stream = File.OpenRead(path);
                return Read(stream, seed, created);
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        public static void Save(string world, PopulationSnapshot snapshot, int seed, long created)
        {
            string path = FilePath(world);
            snapshot.Validate(seed, created);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AtomicFile.Write(path, stream => Write(stream, snapshot));
        }

        public static PopulationSnapshot Read(Stream stream, int seed, long created)
        {
            Require(stream.Length >= HeaderBytes && stream.Length <= HeaderBytes + ColonyBytes * MaxColonies + AgentBytes * MaxAgents, "invalid file size");
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
            Require(r.ReadUInt32() == Magic && r.ReadInt32() == Version, "unknown format or version");
            var s = new PopulationSnapshot
            {
                Seed = r.ReadInt32(), WorldCreated = r.ReadInt64(), Captured = r.ReadInt64(),
                LastEntityId = r.ReadInt32(), LastColonyId = r.ReadInt32()
            };
            int nc = r.ReadInt32(), na = r.ReadInt32();
            Require((uint)nc <= MaxColonies && (uint)na <= MaxAgents &&
                stream.Length == HeaderBytes + nc * ColonyBytes + na * AgentBytes, "invalid record counts or length");
            Require(s.Seed == seed && s.WorldCreated == created, "world binding mismatch");
            s.Colonies = new Colony[nc];
            s.Agents = new Agent[na];
            for (int i = 0; i < nc; i++)
                s.Colonies[i] = new Colony
                {
                    Id = r.ReadInt32(), Leader = r.ReadInt32(), Wild = ReadFlag(r), Arm = r.ReadByte(),
                    X = r.ReadSingle(), Z = r.ReadSingle(), Storage = r.ReadSingle(), Hunger = r.ReadSingle(),
                    LearningRate = r.ReadSingle(), GradientNorm = r.ReadSingle(), Entropy = r.ReadSingle(), ElectionRemaining = r.ReadDouble()
                };
            for (int i = 0; i < na; i++)
            {
                ref var a = ref s.Agents[i];
                a.Id = r.ReadInt32();
                a.ColonyId = r.ReadInt32();
                a.Parent = r.ReadInt32();
                a.Position = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                a.Age = r.ReadSingle();
                a.Health = r.ReadSingle();
                a.Energy = r.ReadSingle();
                a.Stomach = r.ReadSingle();
                a.Carrying = r.ReadSingle();
                a.AttackRemaining = r.ReadDouble();
                a.ReviewRemaining = r.ReadDouble();
                a.Caste = r.ReadByte();
                a.Fixed = ReadFlag(r);
                a.Genes = new float[GeneCount];
                for (int g = 0; g < GeneCount; g++) a.Genes[g] = r.ReadSingle();
            }
            s.Validate(seed, created);
            return s;
        }

        private static bool ReadFlag(BinaryReader r)
        {
            byte flag = r.ReadByte();
            Require(flag <= 1, "invalid flag");
            return flag == 1;
        }

        public static void Write(Stream stream, PopulationSnapshot s)
        {
            using var w = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
            w.Write(Magic);
            w.Write(Version);
            w.Write(s.Seed);
            w.Write(s.WorldCreated);
            w.Write(s.Captured);
            w.Write(s.LastEntityId);
            w.Write(s.LastColonyId);
            w.Write(s.Colonies.Length);
            w.Write(s.Agents.Length);
            foreach (var c in s.Colonies)
            {
                w.Write(c.Id);
                w.Write(c.Leader);
                w.Write(c.Wild);
                w.Write(c.Arm);
                w.Write(c.X);
                w.Write(c.Z);
                w.Write(c.Storage);
                w.Write(c.Hunger);
                w.Write(c.LearningRate);
                w.Write(c.GradientNorm);
                w.Write(c.Entropy);
                w.Write(c.ElectionRemaining);
            }
            foreach (var a in s.Agents)
            {
                w.Write(a.Id);
                w.Write(a.ColonyId);
                w.Write(a.Parent);
                w.Write(a.Position.x);
                w.Write(a.Position.y);
                w.Write(a.Position.z);
                w.Write(a.Age);
                w.Write(a.Health);
                w.Write(a.Energy);
                w.Write(a.Stomach);
                w.Write(a.Carrying);
                w.Write(a.AttackRemaining);
                w.Write(a.ReviewRemaining);
                w.Write(a.Caste);
                w.Write(a.Fixed);
                foreach (float gene in a.Genes) w.Write(gene);
            }
        }
    }
}
