using UnityEngine;
using System;
using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class WorldState : IEquatable<WorldState>
    {
        public int seed;
        public int currency;
        public Vec3 playerPosition;
        public int cycleCount;
        public float startTime;
        public bool gameWon;
        public long lastSaveTime;
        public SerializableInventorySlot[] inventory;
        public float[] rulesMultipliers;

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
                inventory = this.inventory != null ? (SerializableInventorySlot[])this.inventory.Clone() : null,
                rulesMultipliers = this.rulesMultipliers != null ? (float[])this.rulesMultipliers.Clone() : null
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
            public readonly Vector3 ToSpawnPoint() => new(x, y + 1f, z);

            public override readonly string ToString() => $"({x}, {y}, {z})";
            public readonly bool IsNull() => x == 0f && y == 0f && z == 0f;
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
                cycleCount == other.cycleCount &&
                MultipliersEqual(rulesMultipliers, other.rulesMultipliers);
        }

        private static bool MultipliersEqual(float[] a, float[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }

            return true;
        }
    }
}