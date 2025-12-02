using UnityEngine;
using System;
using MemoryPack;

namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class WorldSettings : IEquatable<WorldSettings>
    {
        public string worldName = "New World";
        public int autosaveInterval = 120;
        public float timeScale = 1.0f;
        public int maxEntityCount = 1000;
        public int maxParticleCount = 500;
        public bool enableDebugMode = false;
        public bool enableCheats = false;
        public DifficultyLevel difficulty = DifficultyLevel.Normal;
        public long createdTime;
        public long lastModifiedTime;
        public string version = "1.0";

        public WorldSettings()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            createdTime = now;
            lastModifiedTime = now;
        }

        public void CopyFrom(WorldSettings other)
        {
            worldName = other.worldName;
            autosaveInterval = other.autosaveInterval;
            timeScale = other.timeScale;
            maxEntityCount = other.maxEntityCount;
            maxParticleCount = other.maxParticleCount;
            enableDebugMode = other.enableDebugMode;
            enableCheats = other.enableCheats;
            difficulty = other.difficulty;
            version = other.version;
            createdTime = other.createdTime;
            lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public WorldSettings Clone()
        {
            var clone = new WorldSettings();
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

        public bool Equals(WorldSettings other)
        {
            if (other == null) return false;
            
            return worldName == other.worldName &&
                   autosaveInterval == other.autosaveInterval &&
                   Mathf.Approximately(timeScale, other.timeScale) &&
                   maxEntityCount == other.maxEntityCount &&
                   maxParticleCount == other.maxParticleCount &&
                   enableDebugMode == other.enableDebugMode &&
                   enableCheats == other.enableCheats &&
                   difficulty == other.difficulty;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WorldSettings);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(worldName, autosaveInterval, timeScale, 
                                   maxEntityCount, difficulty);
        }

        public static bool operator ==(WorldSettings left, WorldSettings right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(WorldSettings left, WorldSettings right)
        {
            return !Equals(left, right);
        }
    }

    [System.Serializable]
    public enum DifficultyLevel
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Extreme = 3
    }
}