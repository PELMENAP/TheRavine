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

    public unsafe struct ObjectInstInfo
    {
        public const int PlaceholderId = -1;
        public const int NoSpawnConfig = -1;
        public const int MaxSecondary = 8;

        public int     PrefabID;
        public Vector3 Position;
        public int     Amount;
        public int     PrimaryIdx;
        public int     SecondaryCount;
        public int     SpawnConfig;
        public byte    BaseLevel;
        public byte    Levels;
        public fixed int SecondaryIdxs[MaxSecondary];

        public ObjectInstInfo(Vector3 pos, int prefab, int amount)
        {
            Position       = pos;
            PrefabID       = prefab;
            Amount         = amount;
            PrimaryIdx     = -1;
            SecondaryCount = 0;
            SpawnConfig    = NoSpawnConfig;
            BaseLevel      = 0;
            Levels         = 1;
        }
    }
}