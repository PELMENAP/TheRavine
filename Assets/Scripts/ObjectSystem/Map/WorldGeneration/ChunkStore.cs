using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed class ChunkStore
    {
        private const int Magic  = 0x4B435652;
        private const int Format = 1;

        private readonly string _directory;
        private readonly int _seed;
        private readonly List<ObjectInstInfo> _infos = new(64);
        private readonly List<int> _levels = new(64);
        private byte _detailLevel;

        private ChunkStore(string directory, int seed)
        {
            _directory = directory;
            _seed = seed;
        }

        public static ChunkStore ForWorld(string worldId, int seed)
        {
            string directory = Path.Combine(Application.persistentDataPath, "worlds", worldId, "chunks");
            Directory.CreateDirectory(directory);
            return new ChunkStore(directory, seed);
        }

        public bool TrySave(long chunkKey, ChunkData chunk)
        {
            string path = PathOf(chunkKey);
            string temp = path + ".tmp";

            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(Format);
                    writer.Write(_seed);
                    writer.Write(chunkKey);
                    writer.Write(chunk.DetailLevel);
                    writer.Write(chunk.Objects.Length);

                    for (int i = 0; i < chunk.Objects.Length; i++)
                        WriteObject(writer, chunk, chunk.Objects[i]);
                }

                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (IOException e)
            {
                RavineLog.Warning($"[ChunkStore] Save failed for chunk {chunkKey}: {e.Message}");
                return false;
            }
        }

        public bool TryRestore(long chunkKey, ChunkData chunk)
        {
            string path = PathOf(chunkKey);
            if (!File.Exists(path) || !TryRead(path, chunkKey)) return false;

            chunk.ClearObjects();

            int failed = 0;
            for (int i = 0; i < _infos.Count; i++)
                if (!ApplyObject(chunk, _infos[i], _levels[i])) failed++;

            chunk.CommitRestore(_detailLevel);

            if (failed > 0)
                RavineLog.Warning($"[ChunkStore] Chunk {chunkKey}: {failed} objects were not restored");
            return true;
        }

        private string PathOf(long chunkKey) =>
            Path.Combine(_directory, $"{Position2Int.GetX(chunkKey)}_{Position2Int.GetY(chunkKey)}.chunk");

        private static unsafe void WriteObject(BinaryWriter writer, ChunkData chunk, ObjectInstInfo obj)
        {
            writer.Write(obj.PrefabID);
            writer.Write(obj.Position.x);
            writer.Write(obj.Position.y);
            writer.Write(obj.Position.z);
            writer.Write(obj.Amount);
            writer.Write(obj.PrimaryIdx);
            writer.Write(obj.SpawnConfig);
            writer.Write((byte)chunk.LevelsAt(obj.PrimaryIdx));
            writer.Write((byte)obj.SecondaryCount);

            for (int k = 0; k < obj.SecondaryCount; k++)
                writer.Write(obj.SecondaryIdxs[k]);
        }

        private static unsafe bool ApplyObject(ChunkData chunk, ObjectInstInfo info, int levels) =>
            chunk.TryAddObject(info.PrimaryIdx, in info, new ReadOnlySpan<int>(info.SecondaryIdxs, info.SecondaryCount), levels);

        private unsafe bool TryRead(string path, long chunkKey)
        {
            _infos.Clear();
            _levels.Clear();

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(path))))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != Format || reader.ReadInt32() != _seed || reader.ReadInt64() != chunkKey)
                    {
                        RavineLog.Warning($"[ChunkStore] Chunk {chunkKey}: header mismatch, file ignored");
                        return false;
                    }

                    _detailLevel = reader.ReadByte();
                    int count = reader.ReadInt32();
                    if (count < 0 || count > ChunkData.TotalCells) return false;

                    for (int i = 0; i < count; i++)
                    {
                        int prefab = reader.ReadInt32();
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();
                        int amount = reader.ReadInt32();
                        int primary = reader.ReadInt32();
                        int spawnConfig = reader.ReadInt32();
                        int levels = reader.ReadByte();
                        int secondaryCount = reader.ReadByte();

                        if (primary < 0 || primary >= ChunkData.TotalCells) return false;
                        if (levels < 1 || secondaryCount > ObjectInstInfo.MaxSecondary) return false;

                        var info = new ObjectInstInfo(new Vector3(x, y, z), prefab, amount);
                        info.PrimaryIdx = primary;
                        info.SpawnConfig = spawnConfig;
                        info.SecondaryCount = secondaryCount;
                        for (int k = 0; k < secondaryCount; k++)
                            info.SecondaryIdxs[k] = reader.ReadInt32();

                        _infos.Add(info);
                        _levels.Add(levels);
                    }
                }
                return true;
            }
            catch (IOException e)
            {
                RavineLog.Warning($"[ChunkStore] Read failed for chunk {chunkKey}: {e.Message}");
                return false;
            }
        }
    }
}
