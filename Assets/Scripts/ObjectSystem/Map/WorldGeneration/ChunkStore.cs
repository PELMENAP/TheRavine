using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;

using TheRavine.Base;
using TheRavine.Extensions;

namespace TheRavine.Generator
{
    public sealed class ChunkStore
    {
        private const int Magic  = 0x4B435652;
        private const int Format = 4;
        private const int MinFormat = 1;

        private string _directory;
        private readonly int _seed;
        private readonly List<ObjectInstInfo> _infos = new(64);
        private readonly List<int> _editCells = new(16);
        private readonly List<float> _editHeights = new(16);
        private byte _detailLevel;
        private int _erosionEpoch;
        private long _lastTick;
        private long _objectTick;
        private bool _hasClocks;

        private ChunkStore(string directory, int seed)
        {
            _directory = directory;
            _seed = seed;
        }

        public static ChunkStore ForWorld(string worldId, int seed)
        {
            string directory = DirectoryFor(worldId);
            Directory.CreateDirectory(directory);
            return new ChunkStore(directory, seed);
        }

        public static string DirectoryFor(string worldId) =>
            Path.Combine(WorldRegistry.GetWorldDirectory(worldId), "chunks");

        public void Rebind(string worldId)
        {
            _directory = DirectoryFor(worldId);
            Directory.CreateDirectory(_directory);
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
                    writer.Write(chunk.ErosionEpoch);
                    writer.Write(chunk.LastTick);
                    writer.Write(chunk.ObjectTick);
                    writer.Write(chunk.Objects.Length);

                    for (int i = 0; i < chunk.Objects.Length; i++)
                        WriteObject(writer, chunk, chunk.Objects[i]);

                    var edits = chunk.HeightEdits.GetKeyValueArrays(Allocator.Temp);
                    writer.Write(edits.Length);
                    for (int i = 0; i < edits.Length; i++)
                    {
                        writer.Write(edits.Keys[i]);
                        writer.Write(edits.Values[i]);
                    }
                    edits.Dispose();
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

        public int PeekErosionEpoch(long chunkKey)
        {
            string path = PathOf(chunkKey);
            if (!File.Exists(path)) return 0;

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Magic) return 0;

                    int format = reader.ReadInt32();
                    if (format < MinFormat || format > Format || reader.ReadInt32() != _seed || reader.ReadInt64() != chunkKey) return 0;

                    reader.ReadByte();
                    return format >= 4 ? Math.Max(reader.ReadInt32(), 1) : 1;
                }
            }
            catch (IOException)
            {
                return 0;
            }
        }

        public bool TryRestore(long chunkKey, ChunkData chunk, long worldTick = 0L)
        {
            string path = PathOf(chunkKey);
            if (!File.Exists(path) || !TryRead(path, chunkKey)) return false;

            chunk.ClearObjects();

            int failed = 0;
            for (int i = 0; i < _infos.Count; i++)
                if (!ApplyObject(chunk, _infos[i])) failed++;

            for (int i = 0; i < _editCells.Count; i++)
                chunk.RestoreHeight(_editCells[i], _editHeights[i]);

            chunk.CommitRestore(_detailLevel);
            chunk.ErosionEpoch = _erosionEpoch;
            chunk.LastTick = _lastTick;
            chunk.ObjectTick = _hasClocks ? _objectTick : worldTick;

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
            writer.Write(obj.BaseLevel);
            writer.Write((byte)Math.Max((int)obj.Levels, 1));
            writer.Write((byte)obj.SecondaryCount);

            for (int k = 0; k < obj.SecondaryCount; k++)
                writer.Write(obj.SecondaryIdxs[k]);
        }

        private static unsafe bool ApplyObject(ChunkData chunk, ObjectInstInfo info) =>
            chunk.TryAddObject(info.PrimaryIdx, in info, new ReadOnlySpan<int>(info.SecondaryIdxs, info.SecondaryCount), info.Levels, info.BaseLevel);

        private unsafe bool TryRead(string path, long chunkKey)
        {
            _infos.Clear();
            _editCells.Clear();
            _editHeights.Clear();
            _erosionEpoch = 1;
            _lastTick = 0L;
            _objectTick = 0L;
            _hasClocks = false;

            try
            {
                using (var reader = new BinaryReader(new MemoryStream(File.ReadAllBytes(path))))
                {
                    int magic = reader.ReadInt32();
                    int format = reader.ReadInt32();
                    if (magic != Magic || format < MinFormat || format > Format || reader.ReadInt32() != _seed || reader.ReadInt64() != chunkKey)
                    {
                        RavineLog.Warning($"[ChunkStore] Chunk {chunkKey}: header mismatch, file ignored");
                        return false;
                    }

                    _detailLevel = reader.ReadByte();
                    if (format >= 4)
                    {
                        _erosionEpoch = Math.Max(reader.ReadInt32(), 1);
                        _lastTick = reader.ReadInt64();
                        _objectTick = reader.ReadInt64();
                        _hasClocks = true;
                    }

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
                        int baseLevel = format >= 2 ? reader.ReadByte() : 0;
                        int levels = reader.ReadByte();
                        int secondaryCount = reader.ReadByte();

                        if (primary < 0 || primary >= ChunkData.TotalCells) return false;
                        if (levels < 1 || secondaryCount > ObjectInstInfo.MaxSecondary) return false;
                        if (baseLevel + levels > ObjectInfo.MaxLevels) return false;

                        var info = new ObjectInstInfo(new Vector3(x, y, z), prefab, amount);
                        info.PrimaryIdx = primary;
                        info.SpawnConfig = spawnConfig;
                        info.SecondaryCount = secondaryCount;
                        info.BaseLevel = (byte)baseLevel;
                        info.Levels = (byte)levels;
                        for (int k = 0; k < secondaryCount; k++)
                            info.SecondaryIdxs[k] = reader.ReadInt32();

                        _infos.Add(info);
                    }

                    if (format >= 3)
                    {
                        int editCount = reader.ReadInt32();
                        if (editCount < 0 || editCount > ChunkData.TotalCells) return false;

                        for (int i = 0; i < editCount; i++)
                        {
                            int cell = reader.ReadInt32();
                            float height = reader.ReadSingle();
                            if (cell < 0 || cell >= ChunkData.TotalCells) return false;

                            _editCells.Add(cell);
                            _editHeights.Add(height);
                        }
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
