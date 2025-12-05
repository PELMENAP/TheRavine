using System;

namespace TheRavine.Base
{
    [Serializable]
    public class WorldInfo
    {
        public string Name { get; set; }
        public int Seed { get; set; }
        public DateTimeOffset LastSaveTime { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public int CycleCount { get; set; }
        public bool IsGameWon { get; set; }
        public WorldConfiguration Settings { get; set; }
        public bool IsCurrentWorld { get; set; }
        
        public string GetDisplayName()
        {
            return IsCurrentWorld ? $"{Name} (Текущий)" : Name;
        }
        
        public string GetLastSaveText()
        {
            var now = DateTimeOffset.Now;
            var diff = now - LastSaveTime;
            
            return diff.TotalMinutes < 1 ? "Только что" :
                   diff.TotalHours < 1 ? $"{(int)diff.TotalMinutes} мин назад" :
                   diff.TotalDays < 1 ? $"{(int)diff.TotalHours} ч назад" :
                   diff.TotalDays < 30 ? $"{(int)diff.TotalDays} дн назад" :
                   LastSaveTime.ToString("dd.MM.yyyy");
        }
    }
}