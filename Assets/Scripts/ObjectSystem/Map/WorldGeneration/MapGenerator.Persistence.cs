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

    public partial class MapGenerator
    {
        private void FlushDirty()
        {
            if (dirtyCount == 0 || endless == null) return;

            for (int i = 0; i < dirtyCount; i++)
            for (int e = 0; e < endless.Length; e++)
                endless[e]?.OnChunkDirty(dirtyKeys[i]);

            dirtyCount = 0;
        }

        private void BindChunkStore(WorldRegistry registry)
        {
            boundRegistry = registry;
            boundWorldId = registry.GetCurrentWorldId();
            chunkStore = ChunkStore.ForWorld(boundWorldId, seed, Ravine);
            registry.WorldRenamed += OnWorldRenamed;
        }

        private void OnWorldRenamed(string oldId, string newId)
        {
            if (chunkStore == null || oldId != boundWorldId) return;

            boundWorldId = newId;
            chunkStore.Rebind(newId);
        }

        private void SaveModifiedChunks(bool checkpoint = false)
        {
            if (chunkStore == null) return;

            foreach (var kv in mapData)
            {
                ChunkData cd = kv.Value;
                if (!cd.NeedsSave) continue;

                randomTicks?.StampActive(kv.Key, cd);
                if (chunkStore.TrySave(kv.Key, cd))
                    cd.MarkPersisted();
                else if (checkpoint) throw new System.IO.IOException($"Failed to save chunk {kv.Key}");
            }
        }
    }
}