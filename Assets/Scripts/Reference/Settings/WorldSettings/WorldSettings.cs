using UnityEngine;
using System;

namespace TheRavine.Base
{
    [System.Serializable]
    public class WorldSettings : IEquatable<WorldSettings>
    {
        [Header("Основные настройки")]
        public string worldName = "New World";
        
        [Header("Автосохранение")]
        [Range(0, 600)] public int autosaveInterval = 120;
        
        [Header("Время и производительность")]
        [Range(0.1f, 5.0f)] public float timeScale = 1.0f;
        
        [Header("Лимиты производительности")]
        [Range(100, 5000)] public int maxEntityCount = 1000;
        [Range(50, 1000)] public int maxParticleCount = 500;
        
        [Header("Дополнительные настройки")]
        public bool enableDebugMode = false;
        public bool enableCheats = false;
        public DifficultyLevel difficulty = DifficultyLevel.Normal;
        
        [Header("Метаданные")]
        public long createdTime;
        public long lastModifiedTime;
        public string version = "1.0";

        public WorldSettings()
        {
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            createdTime = now;
            lastModifiedTime = now;
        }

        public WorldSettings Clone()
        {
            var cloned = JsonUtility.FromJson<WorldSettings>(JsonUtility.ToJson(this));
            cloned.lastModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            return cloned;
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

        public string GetAutosaveIntervalText()
        {
            return autosaveInterval switch
            {
                0 => "Отключено",
                < 60 => $"{autosaveInterval} сек",
                < 3600 => $"{autosaveInterval / 60} мин",
                _ => $"{autosaveInterval / 3600} ч"
            };
        }

        public string GetDifficultyText()
        {
            return difficulty switch
            {
                DifficultyLevel.Easy => "Легкий",
                DifficultyLevel.Normal => "Нормальный",
                DifficultyLevel.Hard => "Сложный",
                DifficultyLevel.Extreme => "Экстремальный",
                _ => "Неизвестно"
            };
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