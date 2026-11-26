using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Events;

using TheRavine.Extensions;
using TheRavine.Base;
using TheRavine.ObjectControl;
using TheRavine.EntityControl;
using TheRavine.Events;
using TheRavine.ViewControl;
using R3;

namespace TheRavine.Generator
{
    using EndlessGenerators;
    using TheRavine.Base;
    using Unity.Mathematics;

    public sealed class ChunkData : IDisposable
    {
        private const int Size       = MapGenerator.mapChunkSize;
        public  const int TotalCells = Size * Size;
        public  const byte NotGenerated = byte.MaxValue;

        public NativeArray<float>          HeightRaw;
        public readonly NativeArray<int>   BiomeMap;
        public NativeArray<int>            Occupancy;
        public NativeArray<byte>           LevelMask;
        public NativeParallelHashMap<int, int> UpperHandles;
        public NativeParallelHashMap<int, float> HeightEdits;
        public NativeList<ObjectInstInfo>  Objects;
        public NativeArray<int>            ElevatedCount;
        private Dictionary<int, BuildingVolume> buildings;
        public NativeArray<byte>           MoveCost;
        public NativeArray<float2>         Climate;
        public NativeArray<float>          RiverBlend;
        public NativeArray<byte>           WaterDistance;
        public float WaterLevelStamp;
        public int   WaterRingStamp;
        public int   WaterHoodVersion;
        public int   WaterHoodMask;
        public List<StructureSpawnPoint> StructureSpawnPoints;

        private static bool[] foodTable = Array.Empty<bool>();

        public static void SetFoodTable(bool[] table) => foodTable = table ?? Array.Empty<bool>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFoodPrefab(int prefabId) => (uint)prefabId < (uint)foodTable.Length && foodTable[prefabId];

        public bool IsDirty { get; private set; }
        public int Version { get; private set; }
        public int FoodRevision { get; private set; }
        public int Epoch { get; private set; }
        public int GeneratedVersion { get; private set; }
        public bool IsModified => Version != GeneratedVersion;
        public bool NeedsSave => IsModified && Version != SavedVersion;
        public int SavedVersion { get; private set; }
        public byte DetailLevel { get; internal set; } = NotGenerated;
        public int LastAccessFrame;
        public long LastTick;
        public long ObjectTick;
        public int ErosionEpoch;
        public int SpreadOwed;

        public ChunkData()
        {
            HeightRaw      = new NativeArray<float> (TotalCells, Allocator.Persistent);
            BiomeMap       = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            Occupancy      = new NativeArray<int>   (TotalCells, Allocator.Persistent);
            LevelMask      = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            UpperHandles   = new NativeParallelHashMap<int, int>(16, Allocator.Persistent);
            HeightEdits    = new NativeParallelHashMap<int, float>(16, Allocator.Persistent);
            MoveCost       = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            Climate        = new NativeArray<float2>(TotalCells, Allocator.Persistent);
            RiverBlend     = new NativeArray<float> (TotalCells, Allocator.Persistent);
            WaterDistance  = new NativeArray<byte>  (TotalCells, Allocator.Persistent);
            Objects        = new NativeList<ObjectInstInfo>(16, Allocator.Persistent);
            ElevatedCount  = new NativeArray<int>   (1, Allocator.Persistent);
        }

        public unsafe ObstacleChunk ObstacleView() => new ObstacleChunk
        {
            Occupancy = (int*)Occupancy.GetUnsafeReadOnlyPtr(),
            LevelMask = (byte*)LevelMask.GetUnsafeReadOnlyPtr(),
            Elevated  = (int*)ElevatedCount.GetUnsafeReadOnlyPtr(),
            Objects   = Objects.GetUnsafeList()
        };

        public int BuildingCount => buildings != null ? buildings.Count : 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs = default, int levels = 1)
            => TryAddObject(primaryIdx, in info, additionalIdxs, levels, out _);

        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs, int levels, out PlacementResult result)
            => TryAddObject(primaryIdx, in info, additionalIdxs, levels, 0, out result);

        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs, int levels, int baseLevel)
            => TryAddObject(primaryIdx, in info, additionalIdxs, levels, baseLevel, out _);

        public bool TryAddObject(int primaryIdx, in ObjectInstInfo info, ReadOnlySpan<int> additionalIdxs, int levels, int baseLevel, out PlacementResult result)
        {
            result = Validate(primaryIdx, additionalIdxs, levels, baseLevel);
            if (result != PlacementResult.Ok) return false;

            int secCount = additionalIdxs.Length;
            ObjectInstInfo stored = info;
            stored.PrimaryIdx     = primaryIdx;
            stored.SecondaryCount = secCount;
            stored.BaseLevel      = (byte)baseLevel;
            stored.Levels         = (byte)levels;

            unsafe
            {
                for (int i = 0; i < secCount; i++)
                    stored.SecondaryIdxs[i] = additionalIdxs[i];
            }

            int handle = Objects.Length + 1;
            Objects.Add(stored);

            if (baseLevel == 0)
            {
                Occupancy[primaryIdx] = handle;

                for (int i = 0; i < secCount; i++)
                    Occupancy[additionalIdxs[i]] = -handle;
            }
            else
            {
                ElevatedCount[0] = ElevatedCount[0] + 1;
            }

            if (baseLevel + levels > 1) RegisterUpper(primaryIdx, additionalIdxs, baseLevel, levels, handle);
            if (baseLevel == 0 && levels > 1) AddVolume(primaryIdx, additionalIdxs, levels);

            IsDirty = true;
            Version++;
            if (IsFoodPrefab(info.PrefabID)) FoodRevision++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte SpanMask(int baseLevel, int levels) => (byte)(((1 << levels) - 1) << baseLevel);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int UpperKey(int cellIdx, int level) => (cellIdx << 3) | level;

        private PlacementResult Validate(int primaryIdx, ReadOnlySpan<int> additionalIdxs, int levels, int baseLevel)
        {
            bool ground = baseLevel == 0;
            if ((uint)primaryIdx >= TotalCells) return PlacementResult.OutOfBounds;
            if (ground && Occupancy[primaryIdx] != 0) return PlacementResult.Occupied;

            int secCount = additionalIdxs.Length;
            if (secCount > ObjectInstInfo.MaxSecondary) return PlacementResult.TooManyCells;

            for (int i = 0; i < secCount; i++)
            {
                int ac = additionalIdxs[i];
                if ((uint)ac >= TotalCells) return PlacementResult.OutOfBounds;
                if (ac == primaryIdx || (ground && Occupancy[ac] != 0)) return PlacementResult.Occupied;
                for (int j = 0; j < i; j++)
                    if (additionalIdxs[j] == ac) return PlacementResult.Occupied;
            }

            if (levels < 1 || baseLevel < 0 || baseLevel + levels > ObjectInfo.MaxLevels) return PlacementResult.InvalidLevels;

            byte upper = (byte)(SpanMask(baseLevel, levels) & 0xFE);
            if (upper != 0 && !UpperLevelsFree(primaryIdx, additionalIdxs, upper)) return PlacementResult.UpperLevelOccupied;
            return PlacementResult.Ok;
        }

        private bool UpperLevelsFree(int primaryIdx, ReadOnlySpan<int> additionalIdxs, byte upperMask)
        {
            if ((LevelMask[primaryIdx] & upperMask) != 0) return false;
            for (int i = 0; i < additionalIdxs.Length; i++)
                if ((LevelMask[additionalIdxs[i]] & upperMask) != 0) return false;
            return true;
        }

        private void RegisterUpper(int primaryIdx, ReadOnlySpan<int> additionalIdxs, int baseLevel, int levels, int handle)
        {
            int first = baseLevel > 0 ? baseLevel : 1;
            for (int l = first; l < baseLevel + levels; l++)
            {
                byte bit = (byte)(1 << l);
                UpperHandles[UpperKey(primaryIdx, l)] = handle;
                SetLevelBit(primaryIdx, bit);
                for (int i = 0; i < additionalIdxs.Length; i++)
                {
                    UpperHandles[UpperKey(additionalIdxs[i], l)] = handle;
                    SetLevelBit(additionalIdxs[i], bit);
                }
            }
        }

        private unsafe void UnregisterUpper(in ObjectInstInfo obj)
        {
            int first = obj.BaseLevel > 0 ? obj.BaseLevel : 1;
            for (int l = first; l < obj.BaseLevel + obj.Levels; l++)
            {
                byte mask = (byte)~(1 << l);
                UpperHandles.Remove(UpperKey(obj.PrimaryIdx, l));
                LevelMask[obj.PrimaryIdx] &= mask;
                for (int i = 0; i < obj.SecondaryCount; i++)
                {
                    UpperHandles.Remove(UpperKey(obj.SecondaryIdxs[i], l));
                    LevelMask[obj.SecondaryIdxs[i]] &= mask;
                }
            }
        }

        private unsafe void RemapUpper(in ObjectInstInfo obj, int handle)
        {
            int first = obj.BaseLevel > 0 ? obj.BaseLevel : 1;
            for (int l = first; l < obj.BaseLevel + obj.Levels; l++)
            {
                UpperHandles[UpperKey(obj.PrimaryIdx, l)] = handle;
                for (int i = 0; i < obj.SecondaryCount; i++)
                    UpperHandles[UpperKey(obj.SecondaryIdxs[i], l)] = handle;
            }
        }

        private void AddVolume(int anchor, ReadOnlySpan<int> additionalIdxs, int levels)
        {
            var cells = new LevelCell[(1 + additionalIdxs.Length) * levels];
            int n = 0;

            for (int l = 0; l < levels; l++)
            {
                byte level = (byte)l;
                byte bit   = (byte)(1 << l);

                cells[n++] = new LevelCell(anchor, level);
                if (l > 0) SetLevelBit(anchor, bit);

                for (int i = 0; i < additionalIdxs.Length; i++)
                {
                    cells[n++] = new LevelCell(additionalIdxs[i], level);
                    if (l > 0) SetLevelBit(additionalIdxs[i], bit);
                }
            }

            buildings ??= new Dictionary<int, BuildingVolume>();
            buildings[anchor] = new BuildingVolume(anchor, levels, cells);
        }

        private void SetLevelBit(int cellIdx, byte bit) => LevelMask[cellIdx] = (byte)(LevelMask[cellIdx] | bit);

        private void ClearVolume(BuildingVolume volume)
        {
            for (int i = 0; i < volume.Cells.Length; i++)
            {
                LevelCell cell = volume.Cells[i];
                if (cell.Level == 0) continue;
                LevelMask[cell.LocalIdx] = (byte)(LevelMask[cell.LocalIdx] & ~(1 << cell.Level));
            }
        }

        public bool TryGetBuilding(int anchorIdx, out BuildingVolume volume)
        {
            volume = null;
            return buildings != null && buildings.TryGetValue(anchorIdx, out volume);
        }

        public bool HasLevelAt(int cellIdx, int level)
        {
            if ((uint)cellIdx >= TotalCells || (uint)level >= (uint)ObjectInfo.MaxLevels) return false;
            if (level == 0) return Occupancy[cellIdx] != 0;
            return (LevelMask[cellIdx] & (1 << level)) != 0 || UpperHandles.ContainsKey(UpperKey(cellIdx, level));
        }

        public bool TryGetObject(int cellIdx, int level, out ObjectInstInfo info)
        {
            info = default;
            if (level == 0) return TryGetObject(cellIdx, out info);
            if ((uint)cellIdx >= TotalCells || (uint)level >= (uint)ObjectInfo.MaxLevels) return false;
            if (!UpperHandles.TryGetValue(UpperKey(cellIdx, level), out int handle)) return false;

            int objIdx = handle - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            info = Objects[objIdx];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetObject(int cellIdx, out ObjectInstInfo info)
        {
            info = default;
            if ((uint)cellIdx >= TotalCells) return false;

            int h = Occupancy[cellIdx];
            if (h == 0) return false;

            int objIdx = (h > 0 ? h : -h) - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            info = Objects[objIdx];
            return true;
        }
        public bool RemoveObject(int cellIdx) => RemoveObject(cellIdx, 0);

        public bool RemoveObject(int cellIdx, int level)
        {
            if ((uint)cellIdx >= TotalCells || (uint)level >= (uint)ObjectInfo.MaxLevels) return false;

            int primaryHandle;
            if (level == 0)
            {
                int h = Occupancy[cellIdx];
                if (h == 0) return false;
                primaryHandle = h > 0 ? h : -h;
            }
            else if (!UpperHandles.TryGetValue(UpperKey(cellIdx, level), out primaryHandle))
            {
                return false;
            }

            int objIdx = primaryHandle - 1;
            if ((uint)objIdx >= (uint)Objects.Length) return false;

            ObjectInstInfo obj = Objects[objIdx];

            if (obj.BaseLevel == 0 && buildings != null && buildings.Remove(obj.PrimaryIdx, out BuildingVolume volume))
                ClearVolume(volume);

            if (obj.BaseLevel == 0)
            {
                Occupancy[obj.PrimaryIdx] = 0;
                unsafe
                {
                    for (int i = 0; i < obj.SecondaryCount; i++)
                        Occupancy[obj.SecondaryIdxs[i]] = 0;
                }
            }
            else
            {
                ElevatedCount[0] = ElevatedCount[0] - 1;
            }
            UnregisterUpper(in obj);

            int last = Objects.Length - 1;
            if (objIdx != last)
            {
                ObjectInstInfo tail = Objects[last];
                int newH = objIdx + 1;

                if (tail.BaseLevel == 0)
                {
                    Occupancy[tail.PrimaryIdx] = newH;
                    unsafe
                    {
                        for (int i = 0; i < tail.SecondaryCount; i++)
                            Occupancy[tail.SecondaryIdxs[i]] = -newH;
                    }
                }
                RemapUpper(in tail, newH);

                Objects[objIdx] = tail;
            }

            Objects.RemoveAt(last);
            IsDirty = true;
            Version++;
            if (IsFoodPrefab(obj.PrefabID)) FoodRevision++;
            return true;
        }

        public void ClearDirty() => IsDirty = false;

        public void ClearObjects()
        {
            unsafe
            {
                UnsafeUtility.MemClear(Occupancy.GetUnsafePtr(), (long)TotalCells * sizeof(int));
                UnsafeUtility.MemClear(LevelMask.GetUnsafePtr(), TotalCells);
            }

            for (int i = 0; i < Objects.Length; i++)
            {
                if (!IsFoodPrefab(Objects[i].PrefabID)) continue;
                FoodRevision++;
                break;
            }

            Objects.Clear();
            ElevatedCount[0] = 0;
            if (UpperHandles.IsCreated) UpperHandles.Clear();
            buildings?.Clear();
            Version++;
            IsDirty = true;
        }

        public bool SetHeight(int cellIdx, float height)
        {
            if ((uint)cellIdx >= TotalCells || HeightRaw[cellIdx] == height) return false;

            HeightRaw[cellIdx] = height;
            HeightEdits[cellIdx] = height;
            WaterRingStamp = 0;
            IsDirty = true;
            Version++;
            return true;
        }

        internal void RestoreHeight(int cellIdx, float height)
        {
            if ((uint)cellIdx >= TotalCells) return;

            HeightRaw[cellIdx] = height;
            HeightEdits[cellIdx] = height;
            WaterRingStamp = 0;
            Version++;
        }

        public int LevelsAt(int anchorIdx) => TryGetBuilding(anchorIdx, out BuildingVolume volume) ? volume.Levels : 1;

        internal void MarkPersisted() => SavedVersion = Version;

        internal void CommitRestore(byte detailLevel)
        {
            DetailLevel = detailLevel;
            GeneratedVersion = Version;
            SavedVersion = Version;
        }

        internal void ResetForReuse()
        {
            ClearObjects();
            if (HeightEdits.IsCreated) HeightEdits.Clear();
            StructureSpawnPoints?.Clear();
            WaterRingStamp = 0;
            WaterHoodMask = 0;
            LastTick = 0L;
            ObjectTick = 0L;
            ErosionEpoch = 0;
            SpreadOwed = 0;
            Epoch++;
            DetailLevel = NotGenerated;
            IsDirty = false;
            Version++;
            GeneratedVersion = Version;
        }

        internal void CommitErosion(int epoch)
        {
            ErosionEpoch = epoch;
            InvalidateWater();
        }

        internal void InvalidateWater()
        {
            WaterLevelStamp = float.NaN;
            WaterRingStamp = 0;
            WaterHoodVersion = -1;
            WaterHoodMask = 0;
        }

        internal void CommitGeneration(bool wasModified)
        {
            if (!wasModified) GeneratedVersion = Version;
        }

        public void Dispose()
        {
            if (HeightRaw.IsCreated)      HeightRaw.Dispose();
            if (BiomeMap.IsCreated)       BiomeMap.Dispose();
            if (Occupancy.IsCreated)      Occupancy.Dispose();
            if (LevelMask.IsCreated)      LevelMask.Dispose();
            if (UpperHandles.IsCreated)   UpperHandles.Dispose();
            if (HeightEdits.IsCreated)    HeightEdits.Dispose();
            if (MoveCost.IsCreated)       MoveCost.Dispose();
            if (Climate.IsCreated)        Climate.Dispose();
            if (RiverBlend.IsCreated)     RiverBlend.Dispose();
            if (WaterDistance.IsCreated)  WaterDistance.Dispose();
            if (Objects.IsCreated)        Objects.Dispose();
            if (ElevatedCount.IsCreated)  ElevatedCount.Dispose();
        }
    }
}