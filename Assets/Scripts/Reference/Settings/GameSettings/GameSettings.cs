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

        public void CopyFrom(GameSettings other)
        {
            qualityLevel = other.qualityLevel;
            enableShadows = other.enableShadows;
            enableParticles = other.enableParticles;
            enableProfiling = other.enableProfiling;
            controlType = other.controlType;
        }

        public GameSettings Clone()
        {
            var clone = new GameSettings();
            clone.CopyFrom(this);
            return clone;
        }
    }

    public enum ControlType
    {
        Personal,
        Mobile
    }
}