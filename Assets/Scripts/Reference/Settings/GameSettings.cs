using UnityEngine;

namespace TheRavine.Base
{
    [System.Serializable]
    public class GameSettings
    {
        public int qualityLevel = 2;
        public bool enableShadows = true;
        public bool enableParticles = true;
        public bool enableProfiling = false;
        public ControlType controlType = ControlType.Personal;
        public GameSettings Clone() => JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(this));
    }

    public enum ControlType
    {
        Personal,
        Mobile
    }
}