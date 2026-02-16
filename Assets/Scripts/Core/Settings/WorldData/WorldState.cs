using UnityEngine;
using System;
using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class WorldState : IEquatable<WorldState>
    {
        public int seed;
        public Vec3 playerPosition;
        public int cycleCount;
        public float startTime;
        public bool gameWon;
        public long lastSaveTime;
        public SerializableInventorySlot[] inventory;

        public WorldState Clone()
        {
            return new WorldState
            {
                seed = this.seed,
                playerPosition = this.playerPosition,
                cycleCount = this.cycleCount,
                startTime = this.startTime,
                gameWon = this.gameWon,
                lastSaveTime = this.lastSaveTime,
                inventory = this.inventory != null ? (SerializableInventorySlot[])this.inventory.Clone() : null
            };
        }

        [Serializable]
        public struct Vec3
        {
            public float x, y, z;

            public Vec3(Vector3 position)
            {
                x = position.x;
                y = position.y;
                z = position.z;
            }

            public readonly Vector3 ToVector3() => new(x, y, z);
            public static implicit operator Vector3(Vec3 vec3) => vec3.ToVector3();
            public static implicit operator Vec3(Vector3 vector3) => new(vector3);
        }

        public bool IsDefault() =>
            seed == 0 &&
            cycleCount == 0 &&
            startTime == 0f &&
            !gameWon &&
            lastSaveTime == 0L &&
            playerPosition.x == 0f &&
            playerPosition.y == 0f &&
            playerPosition.z == 0f &&
            (inventory == null || inventory.Length == 0);

        public bool Equals(WorldState other)
        {
            return seed == other.seed &&
                cycleCount == other.cycleCount;
        }
    }
}