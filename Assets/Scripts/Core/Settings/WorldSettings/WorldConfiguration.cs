using UnityEngine;
using System;
using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class WorldConfiguration : IEquatable<WorldConfiguration>
    {
        public string worldName = "New World";
        public int autosaveInterval = 20;
        public float timeScale = 1.0f;
        public int maxEntityCount = 1000;
        public int maxParticleCount = 500;
        public bool generateStructures = false;
        public bool generateRivers = false;
        public DifficultyLevel difficulty = DifficultyLevel.Normal;
        public long createdTime;
        public long lastModifiedTime;
        public string version = "1.0";

        public WorldConfiguration()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            createdTime = now;
            lastModifiedTime = now;
        }

        public void CopyFrom(WorldConfiguration other)
        {
            worldName = other.worldName;
            autosaveInterval = other.autosaveInterval;
            timeScale = other.timeScale;
            maxEntityCount = other.maxEntityCount;
            maxParticleCount = other.maxParticleCount;
            generateStructures = other.generateStructures;
            generateRivers = other.generateRivers;
            difficulty = other.difficulty;
            version = other.version;
            createdTime = other.createdTime;
            lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public WorldConfiguration Clone()
        {
            var clone = new WorldConfiguration();
            clone.CopyFrom(this);
            return clone;
        }

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(worldName) &&
                   autosaveInterval >= 0 &&
                   timeScale > 0 &&
                   maxEntityCount > 0 &&
                   maxParticleCount > 0;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(worldName))
                worldName = "New World";
            
            autosaveInterval = Mathf.Clamp(autosaveInterval, 0, 600);
            timeScale = Mathf.Clamp(timeScale, 0.1f, 5.0f);
            maxEntityCount = Mathf.Clamp(maxEntityCount, 100, 5000);
            maxParticleCount = Mathf.Clamp(maxParticleCount, 50, 1000);
            
            lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public bool Equals(WorldConfiguration other)
        {
            if (other == null) return false;
            
            return worldName == other.worldName &&
                   autosaveInterval == other.autosaveInterval &&
                   Mathf.Approximately(timeScale, other.timeScale) &&
                   maxEntityCount == other.maxEntityCount &&
                   maxParticleCount == other.maxParticleCount &&
                   generateStructures == other.generateStructures &&
                   generateRivers == other.generateRivers &&
                   difficulty == other.difficulty;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WorldConfiguration);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(worldName, autosaveInterval, timeScale, 
                                   maxEntityCount, difficulty);
        }

        public static bool operator ==(WorldConfiguration left, WorldConfiguration right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WorldConfiguration left, WorldConfiguration right)
        {
            return !Equals(left, right);
        }
    }

    [Serializable]
    public enum DifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Extreme = 3
    }
}