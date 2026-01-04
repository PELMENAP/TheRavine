using MemoryPack;
using System;
namespace TheRavine.Base
{
    [MemoryPackable]
    public partial class GlobalSettings : IEquatable<GlobalSettings>
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

        public bool Equals(GlobalSettings other)
        {
            if (other == null) return false;
            
            return qualityLevel == other.qualityLevel &&
                   enableShadows == other.enableShadows &&
                   enableParticles == other.enableParticles &&
                   enableProfiling == other.enableProfiling &&
                   controlType == other.controlType;
        }
    }

    public enum ControlType
    {
        Personal,
        Mobile
    }
}