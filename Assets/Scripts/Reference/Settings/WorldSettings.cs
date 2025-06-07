using UnityEngine;

namespace TheRavine.Base
{
    [System.Serializable]
    public class WorldSettings
    {
        public string worldName = "New World";
        public int autosaveInterval = 120;
        public float timeScale = 1.0f;
        public bool pauseOnFocusLoss = true;
        public int maxEntityCount = 1000;
        
        public WorldSettings Clone() => JsonUtility.FromJson<WorldSettings>(JsonUtility.ToJson(this));
    }
}