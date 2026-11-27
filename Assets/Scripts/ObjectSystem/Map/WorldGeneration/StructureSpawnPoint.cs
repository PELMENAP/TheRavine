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

    public readonly struct StructureSpawnPoint
    {
        public readonly long WorldPosition;
        public readonly GenerationSettingsSO WfcSettings;
        public readonly TilePatternSO InitialPattern;

        public StructureSpawnPoint(
            long pos,
            GenerationSettingsSO settings,
            TilePatternSO pattern)
        {
            WorldPosition  = pos;
            WfcSettings    = settings;
            InitialPattern = pattern;
        }
    }
}