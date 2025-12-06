namespace TheRavine.Base
{
    [System.Serializable]
    public class GlobalSettings
    {
        public int qualityLevel = 2;
        public bool enableShadows = true;
        public bool enableParticles = true;
        public bool enableProfiling = false;
        public ControlType controlType = ControlType.Personal;

        public void CopyFrom(GlobalSettings other)
        {
            qualityLevel = other.qualityLevel;
            enableShadows = other.enableShadows;
            enableParticles = other.enableParticles;
            enableProfiling = other.enableProfiling;
            controlType = other.controlType;
        }

        public GlobalSettings Clone()
        {
            var clone = new GlobalSettings();
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