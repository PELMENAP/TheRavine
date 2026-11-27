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
        private const int AgentBytesV1 = 62 + sizeof(float) * GeneCount;
        private const int StressBytes = 2 * sizeof(float) + sizeof(byte);
        private const int AgentBytesV2 = AgentBytesV1 + StressBytes;
        private const int HelpEntryBytes = sizeof(int) + 7 * sizeof(float);
        private const int AgentBytesV3Min = AgentBytesV2 + sizeof(byte);
        private const int AgentBytes = AgentBytesV3Min + MaxHelpEntries * HelpEntryBytes;

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
            try
            {
                return ReadRecords(stream, seed, created);
            }
            catch (EndOfStreamException)
            {
                throw new InvalidDataException("truncated data");
            }
        }

        private static PopulationSnapshot ReadRecords(Stream stream, int seed, long created)
        {
            Require(stream.Length >= HeaderBytes && stream.Length <= HeaderBytes + ColonyBytes * MaxColonies + AgentBytes * MaxAgents, "invalid file size");
            using var r = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
            Require(r.ReadUInt32() == Magic, "unknown format or version");
            int version = r.ReadInt32();
            Require(version >= MinVersion && version <= Version, "unknown format or version");
            int agentBytes = version >= 3 ? AgentBytesV3Min : version >= 2 ? AgentBytesV2 : AgentBytesV1;
            var s = new PopulationSnapshot
            {
                Seed = r.ReadInt32(), WorldCreated = r.ReadInt64(), Captured = r.ReadInt64(),
                LastEntityId = r.ReadInt32(), LastColonyId = r.ReadInt32()
            };
            int nc = r.ReadInt32(), na = r.ReadInt32();
            Require((uint)nc <= MaxColonies && (uint)na <= MaxAgents, "invalid record counts or length");
            long fixedLength = HeaderBytes + (long)nc * ColonyBytes + (long)na * agentBytes;
            Require(version >= 3
                ? stream.Length >= fixedLength && stream.Length <= fixedLength + (long)na * MaxHelpEntries * HelpEntryBytes
                : stream.Length == fixedLength, "invalid record counts or length");
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
                if (version >= 2)
                {
                    a.Stress = r.ReadSingle();
                    a.LonelySeconds = r.ReadSingle();
                    a.StressLevel = r.ReadByte();
                }
                if (version >= 3)
                {
                    int helpCount = r.ReadByte();
                    Require(helpCount <= MaxHelpEntries && stream.Length - stream.Position >= (long)helpCount * HelpEntryBytes, "invalid help entry count");
                    a.Help = new HelpRecord[helpCount];
                    for (int h = 0; h < helpCount; h++)
                        a.Help[h] = new HelpRecord
                        {
                            Partner = r.ReadInt32(), Balance = r.ReadSingle(), ComfortAccum = r.ReadSingle(),
                            BalanceAge = r.ReadSingle(), ComfortAge = r.ReadSingle(), GaveAge = r.ReadSingle(),
                            ReceivedAge = r.ReadSingle(), MetAge = r.ReadSingle()
                        };
                }
                a.Genes = new float[GeneCount];
                for (int g = 0; g < GeneCount; g++) a.Genes[g] = r.ReadSingle();
            }
            Require(stream.Position == stream.Length, "trailing data");
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
                w.Write(a.Stress);
                w.Write(a.LonelySeconds);
                w.Write(a.StressLevel);
                int helpCount = a.Help != null ? a.Help.Length : 0;
                Require(helpCount <= MaxHelpEntries, "too many help entries");
                w.Write((byte)helpCount);
                for (int h = 0; h < helpCount; h++)
                {
                    var e = a.Help[h];
                    w.Write(e.Partner);
                    w.Write(e.Balance);
                    w.Write(e.ComfortAccum);
                    w.Write(e.BalanceAge);
                    w.Write(e.ComfortAge);
                    w.Write(e.GaveAge);
                    w.Write(e.ReceivedAge);
                    w.Write(e.MetAge);
                }
                foreach (float gene in a.Genes) w.Write(gene);
            }
        }
    }
}
